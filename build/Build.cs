using System;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.NerdbankGitVersioning;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Tools.DotNet;
using Serilog;
using static Nuke.Common.EnvironmentInfo;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.Tooling.ProcessTasks;
using System.Collections.Generic;
using Nuke.Common.Tools.PowerShell;
using System.Net;
using System.Text.Json;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;

[GitHubActions(
    "BuildOnly",
    GitHubActionsImage.WindowsLatest,
    OnPushBranches = new[] { "main" },
    FetchDepth = 0,
    InvokedTargets = new[] { nameof(Compile) })]
[GitHubActions(
    "BuildDeploy",
    GitHubActionsImage.WindowsLatest,
    OnPullRequestBranches = new[] { "main" },
    FetchDepth = 0,
    ImportSecrets = new[] { nameof(NuGetApiKey) },
    InvokedTargets = new[] { nameof(Compile), nameof(Deploy) })]
partial class Build : NukeBuild
{
    //// Support plugins are available for:
    ////   - JetBrains ReSharper        https://nuke.build/resharper
    ////   - JetBrains Rider            https://nuke.build/rider
    ////   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ////   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Compile);

    [GitRepository] readonly GitRepository Repository;
    [Solution(GenerateProjects = true)] readonly Solution Solution;
    [NerdbankGitVersioning] readonly NerdbankGitVersioning NerdbankVersioning;
    [Parameter][Secret] readonly string NuGetApiKey;

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    AbsolutePath PackagesDirectory => RootDirectory / "output";
    string PublicNuGetSource => "https://api.nuget.org/v3/index.json";

    Target Print => _ => _
        .Executes(() =>
        {
            Log.Information("NerdbankVersioning = {Value}", NerdbankVersioning.NuGetPackageVersion);
        });

    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            if (!IsLocalBuild)
            {
                return;
            }

            PackagesDirectory.CreateOrCleanDirectory();
            UpdateVisualStudio();
            InstallDotNetSdk("6.6.x", "7.7.x");
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore(s => s.SetProjectFile(Solution));

            RestoreSolutionWorkloads(Solution);
        });

    Target Compile => _ => _
        .DependsOn(Restore, Print)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });

    Target Pack => _ => _
    .After(Compile)
    .Requires(() => Repository.IsOnMainOrMasterBranch())
    .Produces(PackagesDirectory / "*.nupkg")
    .Executes(() =>
    {
        var packableProjects = GetPackableProjects();

        packableProjects.ForEach(project =>
        {
            Log.Information("Restoring workloads of {Input}", project);
            RestoreProjectWorkload(project);
        });

        DotNetPack(settings => settings
            .SetConfiguration(Configuration)
            .SetVersion(NerdbankVersioning.NuGetPackageVersion)
            .SetOutputDirectory(PackagesDirectory)
            .CombineWith(packableProjects, (packSettings, project) =>
                packSettings.SetProject(project)));

    });

    Target Deploy => _ => _
    .DependsOn(Pack)
    .Requires(() => NuGetApiKey)
    .Requires(() => Repository.IsOnMainOrMasterBranch())
    .Executes(() =>
    {
        DotNetNuGetPush(settings => settings
                    .SetSource(PublicNuGetSource)
                    .SetApiKey(NuGetApiKey)
                    .CombineWith(PackagesDirectory.GlobFiles("*.nupkg").NotEmpty(), (s, v) => s.SetTargetPath(v)),
                degreeOfParallelism: 5, completeOnFailure: true);
    });

    static void UpdateVisualStudio(string version = "Enterprise")
    {
        StartShell($@"dotnet tool update -g dotnet-vs").AssertZeroExitCode();
        StartShell($@"vs where release").AssertZeroExitCode();
        StartShell($@"vs update release {version}").AssertZeroExitCode();
        StartShell($@"vs modify release {version} +mobile +desktop +uwp +web").AssertZeroExitCode();
        StartShell($@"vs where release").AssertZeroExitCode();
    }

    static void InstallDotNetSdk(params string[] versions)
    {
        var versionsToInstall = new List<int[]>();
        var lookupVersions = versions.Select(v => (v, v.Split('.').Select(x =>
        {
            return x == "x" || !int.TryParse(x, out var i) ? default(int?) : i;
        }).ToArray())).ToList();
        using (var w = new WebClient())
        {
            var json_data = string.Empty;
            // attempt to download JSON data as a string
            try
            {
                json_data = w.DownloadString("https://raw.githubusercontent.com/dotnet/core/main/release-notes/releases-index.json");

                var releasesArray = JsonNode.Parse(json_data).Root["releases-index"].AsArray();

                // Find the closest version to the one we want to install
                foreach (var version in lookupVersions)
                {
                    var closestVersion = releasesArray.Where(x =>
                    {
                        // check if the version is not a preview version and if the major version matches
                        var relver = x["latest-sdk"].ToString();
                        return !relver.Contains("preview") && version.Item2[0].Equals((relver.Split('.').Select(int.Parse).ToArray()[0]));
                    }).OrderBy(x => Math.Abs(x["latest-sdk"].ToString().CompareTo(version.v))).First();
                    var ver = closestVersion["latest-sdk"].ToString();
                    var verSplit = ver.Split('.').Select(int.Parse).ToArray();

                    // check if the version is already in the list
                    if (versionsToInstall.Any(x => x[0] == verSplit[0] && x[1] == verSplit[1] && x[2] == verSplit[2]))
                    {
                        continue;
                    }

                    // check if the version is higher than the one we want to install
                    if (verSplit[1] > version.Item2[1])
                    {
                        if (version.Item2[1].HasValue)
                        {
                            verSplit[1] = version.Item2[1].Value;
                        }
                    }

                    if (verSplit[2] > version.Item2[2])
                    {
                        if (version.Item2[2].HasValue)
                        {
                            verSplit[2] = version.Item2[2].Value;
                        }
                        else
                        {
                            // TODO: if the minor version is not specified, then we want the latest.T
                            // The output must be a string, must be three digits, and must be padded with xx if not three digits
                        }
                    }

                    versionsToInstall.Add(verSplit);
                }

                // if versionsToInstall is empty, then we didn't find any versions to install
                if (versionsToInstall.Count == 0)
                {
                    Log.Information("No matching versions found to install");
                }
            }
            catch (Exception ex)
            {
                Log.Information("Error downloading JSON data: {Value}", ex.Message);
            }
        }

        StartShell($@"powershell -NoProfile -ExecutionPolicy unrestricted -Command Invoke-WebRequest 'https://dot.net/v1/dotnet-install.ps1' -OutFile 'dotnet-install.ps1';").AssertZeroExitCode();
        foreach (var version in versionsToInstall.Select(arr => $"{arr[0]}.{arr[1]}.{arr[2]}").ToArray())
        {
            Console.WriteLine($"Installing .NET SDK {version}");
            StartShell($@"powershell -NoProfile -ExecutionPolicy unrestricted -Command ./dotnet-install.ps1 -Channel '{version}';").AssertZeroExitCode();
        }
    }

    void RestoreProjectWorkload(Project project) => StartShell($@"dotnet workload restore --project {project.Path}").AssertZeroExitCode();

    void RestoreSolutionWorkloads(Solution solution) => StartShell($@"dotnet workload restore {solution}").AssertZeroExitCode();

    List<Project> GetPackableProjects() => Solution.AllProjects.Where(x => x.GetProperty<bool>("IsPackable")).ToList();
}
