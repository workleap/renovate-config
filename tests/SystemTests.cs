using System.Text;
using CliWrap;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Meziantou.Framework;
using Meziantou.Framework.InlineSnapshotTesting;
using Octokit;
using Octokit.Internal;
using Xunit.Abstractions;

namespace renovate_config.tests;

public class SystemTests
{
    private const string GitUrl = "https://github.com/gsoft-inc/renovate-config-test";

    private const string Githubpat = ""; // GH auth token

    private readonly ITestOutputHelper _testOutputHelper;
    private readonly string _uniqueId = $"{DateTime.UtcNow:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString("N").ToLowerInvariant()}";

    private readonly InMemoryCredentialStore credentials = new InMemoryCredentialStore(new Octokit.Credentials(Githubpat));

    public SystemTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task RenovateDotnetSdkDependencies()
    {
        await using var temporaryDirectory = TemporaryDirectory.Create();
        var repoPath = temporaryDirectory.FullPath;
        var branchName = "main";
        await this.ExecuteCommand("git", ["clone", GitUrl, repoPath]);
        await this.ExecuteCommand("git", ["-C", repoPath, "rm", "-rf", "."]);

        this.CopyRenovateFile(temporaryDirectory);
        temporaryDirectory.CreateTextFile("global.json", /*lang=json*/"""{"sdk": {"version": "5.0.100"}}""");

        await this.ExecuteCommand("git", ["-C", repoPath, "add", "."] );
        await this.ExecuteCommand("git", ["-C", repoPath, "commit", "--message", "IDP ScaffoldIt automated test", "--author", "IDP ScaffoldIt <idp@workleap.com>"]);
        await this.ExecuteCommand("git", ["-C", repoPath, "push", "--set-upstream", GitUrl, branchName]);

        await this.ExecuteCommand(
            "docker",
            [
                "run",
                "--rm",
                "-e", "LOG_LEVEL=debug",
                "-e", "RENOVATE_PRINT_CONFIG=true",
                "-e", $"RENOVATE_TOKEN={Githubpat}",
                "-e", "RENOVATE_RECREATE_WHEN=always",
                "-e", "RENOVATE_INHERIT_CONFIG_FILE_NAME=not-renovate.json",
                "-e", "RENOVATE_REPOSITORIES=[\"https://github.com/gsoft-inc/renovate-config-test\"]",
                "renovate/renovate:latest",
                "renovate",
                "gsoft-inc/renovate-config-test"
            ]);

        var githubClient = new GitHubClient(new ProductHeaderValue("renovate-test"), credentials);
        var pullRequests = await githubClient.PullRequest.GetAllForRepository("gsoft-inc", "renovate-config-test", new PullRequestRequest(){ Base = branchName});

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        var pullRequestsInfos = new List<PullRequestInfos>(pullRequests.Count);

        foreach (var pullRequest in pullRequests.OrderBy(x => x.Title))
        {
            MarkdownDocument markdownDocument = Markdown.Parse(pullRequest.Body, pipeline);
            var prTitle = pullRequest.Title;

            var table = markdownDocument.OfType<Table>().First();
            var rows = table.Skip(1).OfType<TableRow>().ToArray();

            var packageUpdateInfos = new List<PackageUpdateInfos>(rows.Length);

            foreach (var row in rows)
            {
                var packge = row[0].InnerText().Trim();
                var type = row[1].InnerText().Trim();
                var update = row[2].InnerText().Trim();

                packageUpdateInfos.Add(new PackageUpdateInfos(packge, type, update));
            }

            pullRequestsInfos.Add(new PullRequestInfos(prTitle, packageUpdateInfos.OrderBy(x => x.Package).ThenBy(x => x.Type).ThenBy(x => x.Update)));
        }

        InlineSnapshot.Validate(pullRequestsInfos.OrderBy(x => x.Title), """
            - Title: chore(deps): update dependency dotnet-sdk to v5.0.408
              PackageUpdatesInfos:
                - Package: dotnet-sdk
                  Type: dotnet-sdk
                  Update: patch
            - Title: chore(deps): update dependency dotnet-sdk to v8
              PackageUpdatesInfos:
                - Package: dotnet-sdk
                  Type: dotnet-sdk
                  Update: major
            """);
    }

    private void CopyRenovateFile(TemporaryDirectory temporaryDirectory)
    {
        var gitRoot = GetGitRoot();

        File.Copy(gitRoot / "default.json", temporaryDirectory.FullPath / "renovate.json");
    }

    private async Task<(string StdOut, string StdError)> ExecuteCommand(string executable, string[] args, KeyValuePair<string, string>[]? envVariables = null)
    {
        var stdOut = new StringBuilder();
        var stdErr = new StringBuilder();
        await Cli.Wrap(executable).WithArguments(args)
            .WithEnvironmentVariables(builder =>
            {
                foreach (var (key, value) in envVariables ?? [])
                {
                    builder.Set(key, value);
                }
            })
            .WithStandardOutputPipe(PipeTarget.Merge(PipeTarget.ToStringBuilder(stdOut), PipeTarget.ToDelegate(_testOutputHelper.WriteLine)))
            .WithStandardErrorPipe(PipeTarget.Merge(PipeTarget.ToStringBuilder(stdErr), PipeTarget.ToDelegate(_testOutputHelper.WriteLine)))
            .ExecuteAsync();

        return (stdOut.ToString(), stdErr.ToString());
    }

    private static FullPath GetGitRoot()
    {
        if (FullPath.CurrentDirectory().TryFindFirstAncestorOrSelf(current => Directory.Exists(current / ".git"), out var root))
        {
            return root;
        }

        throw new InvalidOperationException("root folder not found");
    }
}

public record PullRequestInfos(string Title, IEnumerable<PackageUpdateInfos> PackageUpdatesInfos);

public record PackageUpdateInfos(string Package, string Type, string Update);


static class Extensions
{
    public static string ToNormalizedString(this MarkdownObject obj)
    {
        using var writer = new StringWriter();
        var renderer = new NormalizeRenderer(writer);
        renderer.Render(obj);
        return writer.ToString();
    }
    public static string InnerText(this MarkdownObject obj)
    {
        var inlines = obj.Descendants<LiteralInline>();
        return string.Join(" ", inlines.Select(inline=> inline.ToNormalizedString()));
    }
}