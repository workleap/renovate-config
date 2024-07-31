using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Meziantou.Framework;
using Meziantou.Framework.InlineSnapshotTesting;
using Octokit;
using Octokit.Internal;
using Xunit.Abstractions;

namespace renovate_config.tests;

public class TestContext(ITestOutputHelper outputHelper, TemporaryDirectory temporaryDirectory, GitHubClient gitHubClient)
{
    private const string DefaultBranchName = "main";
    private const string GitUrl = "https://github.com/gsoft-inc/renovate-config-test";
    private readonly string _repoPath = temporaryDirectory.FullPath;

    public static async Task<TestContext> CreateAsync(ITestOutputHelper outputHelper)
    {
        var gitGubClient = await CreateGitHubClient(outputHelper);

        var temporaryDirectory = TemporaryDirectory.Create();
        var repoPath = temporaryDirectory.FullPath;
        await ExecuteCommand(outputHelper, "git", ["-C", repoPath, "init", "--initial-branch=main"]);

        CopyRenovateFile(temporaryDirectory);

        return new TestContext(outputHelper, temporaryDirectory, gitGubClient);
    }

    public void AddFile(string path, string content)
    {
        temporaryDirectory.CreateTextFile(path, content);
    }

    public async Task RunRenovate()
    {
        var token = await GetGitHubToken(outputHelper);

        await this.CleanupRepository();

        await ExecuteCommand(outputHelper, "git", ["-C", _repoPath, "add", "."] );
        await ExecuteCommand(outputHelper, "git", ["-C", _repoPath, "commit", "--message", "IDP ScaffoldIt automated test", "--author", "IDP ScaffoldIt <idp@workleap.com>"]);
        await ExecuteCommand(outputHelper, "git", ["-C", _repoPath, "push", GitUrl, DefaultBranchName + ":" + DefaultBranchName, "--force"]);

        await ExecuteCommand(
            outputHelper,
            "docker",
            [
                "run",
                "--rm",
                "-e", "LOG_LEVEL=debug",
                "-e", "RENOVATE_PRINT_CONFIG=true",
                "-e", $"RENOVATE_TOKEN={token}",
                "-e", "RENOVATE_RECREATE_WHEN=always",
                "-e", "RENOVATE_INHERIT_CONFIG_FILE_NAME=not-renovate.json",
                "-e", "RENOVATE_REPOSITORIES=[\"https://github.com/gsoft-inc/renovate-config-test\"]",
                "renovate/renovate:latest",
                "renovate",
                "gsoft-inc/renovate-config-test"
            ]);
    }

    [InlineSnapshotAssertion(nameof(expected))]
    public async Task AssertPullRequest(string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        var pullRequests = await GetPullRequests();

        InlineSnapshotSettings.Default.ValidateSourceFilePathUsingPdbInfoWhenAvailable = false;
        InlineSnapshot
            .WithSettings(settings => settings.ScrubLinesWithReplace(line => Regex.Replace(line, "to v[^ ]+ ?", "")))
            .Validate(pullRequests, expected, filePath, lineNumber);
    }

    public async Task<IEnumerable<PullRequestInfos>> GetPullRequests()
    {
        var pullRequests = await gitHubClient.PullRequest.GetAllForRepository("gsoft-inc", "renovate-config-test", new PullRequestRequest(){ Base = DefaultBranchName});

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

        return pullRequestsInfos;
    }

    private async Task CleanupRepository()
    {
        var branches = await gitHubClient.Repository.Branch.GetAll("gsoft-inc", "renovate-config-test");
        var branchesToDelete = branches.Where(x => x.Name != DefaultBranchName);

        foreach (var branch in branchesToDelete)
        {
            await gitHubClient.Git.Reference.Delete("gsoft-inc", "renovate-config-test", $"heads/{branch.Name}");
        }
    }

    private static async Task<string> GetGitHubToken(ITestOutputHelper outputHelper)
    {
        var token = Environment.GetEnvironmentVariable("TEST_GITHUB_TOKEN");

        if (string.IsNullOrEmpty(token))
        {
            var (stdout, _) = await ExecuteCommand(outputHelper, "gh", ["auth", "token"]);

            token = stdout.Trim();
        }

        if (string.IsNullOrEmpty(token))
        {
            throw new Exception("GitHub token not found, run `gh auth login` to authenticate");
        }

        return token;
    }

    private static async Task<GitHubClient> CreateGitHubClient(ITestOutputHelper outputHelper)
    {
        var token = await GetGitHubToken(outputHelper);

        var githubClient = new GitHubClient(new ProductHeaderValue("renovate-test"), new InMemoryCredentialStore(new Octokit.Credentials(token)));

        return githubClient;
    }


    private static void CopyRenovateFile(TemporaryDirectory temporaryDirectory)
    {
        var gitRoot = GetGitRoot();

        File.Copy(gitRoot / "default.json", temporaryDirectory.FullPath / "renovate.json");
    }

    private static FullPath GetGitRoot()
    {
        if (FullPath.CurrentDirectory().TryFindFirstAncestorOrSelf(current => Directory.Exists(current / ".git"), out var root))
        {
            return root;
        }

        throw new InvalidOperationException("root folder not found");
    }

    private static async Task<(string StdOut, string StdError)> ExecuteCommand(ITestOutputHelper outputHelper,
        string executable, string[] args, KeyValuePair<string, string>[]? envVariables = null)
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
            .WithStandardOutputPipe(PipeTarget.Merge(PipeTarget.ToStringBuilder(stdOut), PipeTarget.ToDelegate(outputHelper.WriteLine)))
            .WithStandardErrorPipe(PipeTarget.Merge(PipeTarget.ToStringBuilder(stdErr), PipeTarget.ToDelegate(outputHelper.WriteLine)))
            .ExecuteAsync();

        return (stdOut.ToString(), stdErr.ToString());
    }
}