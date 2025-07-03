using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using CliWrap;
using GitHub.Octokit.Client;
using GitHub.Octokit.Client.Authentication;
using Markdig;
using Markdig.Extensions.Tables;
using Markdig.Syntax;
using Meziantou.Framework;
using Meziantou.Framework.InlineSnapshotTesting;
using Octokit;
using Octokit.Internal;
using Xunit.Abstractions;

using NewGitHubClient = GitHub.GitHubClient;
using OldGitHubClient = Octokit.GitHubClient;

namespace renovate_config.tests;

internal sealed class TestContext(
    ITestOutputHelper outputHelper,
    TemporaryDirectory repositoryDirectory,
    TemporaryFeatureBranchName targetBranchName,
    NewGitHubClient newGitHubClient,
    OldGitHubClient oldGitHubClient) : IAsyncDisposable
{
    private static NewGitHubClient? _sharedNewGitHubClient;
    private static OldGitHubClient? _sharedOldGitHubClient;

    private const string RepositoryOwner = "workleap";
    private const string RepositoryName = "renovate-config-test";
    private readonly string _repoPath = repositoryDirectory.FullPath;
    private const string InitialCommitMessage = "IDP ScaffoldIt automated test";

    public static async Task<TestContext> CreateAsync(ITestOutputHelper outputHelper)
    {
        var newGitHubClient = await GetNewGitHubClient(outputHelper);
        var oldGitHubClient = await GetOldGitHubClient(outputHelper);

        await ExecuteCommand(outputHelper, "gh", ["auth", "status"]);

        TemporaryDirectory? repositoryDirectory = null;
        try
        {
            var targetBranchName = TemporaryFeatureBranchName.Create();
            outputHelper.WriteLine($"Target branch name: {targetBranchName}");

            repositoryDirectory = TemporaryDirectory.Create();
            await ExecuteCommand(outputHelper, "git", ["-C", repositoryDirectory.FullPath, "init", $"--initial-branch={targetBranchName}"]);

            CopyRenovateFile(repositoryDirectory);

            return new TestContext(outputHelper, repositoryDirectory, targetBranchName, newGitHubClient, oldGitHubClient);
        }
        catch
        {
            if (repositoryDirectory != null)
            {
                await repositoryDirectory.DisposeAsync();
            }

            throw;
        }
    }

    public void AddFile(string path, string content)
    {
        repositoryDirectory.CreateTextFile(path, content);
    }

    public void AddInternalDeveloperPlatformCodeOwnersFile()
    {
        repositoryDirectory.CreateTextFile("CODEOWNERS",
            """
            * @workleap/internal-developer-platform
            """);
    }

    public void AddSuccessfulWorkflowFileToSatisfyBranchPolicy()
    {
        repositoryDirectory.CreateTextFile(".github/workflows/ci.yml", /*lang=yaml*/ """
            name: CI
            on:
                pull_request: 
                    branches:
                        - "*"
                push:
                    branches:
                        - "renovate/**"
                        
            jobs:
                build:
                    runs-on: ubuntu-latest
                    steps:
                        - name: Dummy successful step
                          run: echo "Hello world"
            """
            );
    }

    public void AddFailingWorkflowFileToSatisfyBranchPolicy()
    {
        repositoryDirectory.CreateTextFile(".github/workflows/ci.yml", /*lang=yaml*/ """
            name: CI
            on:
                pull_request: 
                    branches:
                        - "*"
                push:
                    branches:
                        - "renovate/**"
                        
            jobs:
                build:
                    runs-on: ubuntu-latest
                    steps:
                        - name: Dummy failing step
                          run: exit 1
            """
        );
    }

    public async Task PushFilesOnTemporaryBranch()
    {
        var token = await GetGitHubToken(outputHelper);
        var gitUrl = $"https://{token}@github.com/workleap/renovate-config-test";

        await this.CleanupRepository();

        await ExecuteCommand(outputHelper, "git", ["-C", this._repoPath, "add", "."]);
        await ExecuteCommand(outputHelper, "git", ["-C", this._repoPath, "-c", "user.email=idp@workleap.com", "-c", "user.name=IDP ScaffoldIt", "commit", "--message", InitialCommitMessage]);
        await ExecuteCommand(outputHelper, "git", ["-C", this._repoPath, "push", gitUrl, $"{targetBranchName}:{targetBranchName}"]);
    }

    public void UseRenovateFile(string filename)
    {
        var gitRoot = GetGitRoot();
        var filePath = repositoryDirectory.FullPath / "renovate.json";

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        File.Copy(gitRoot / filename, filePath);
    }

    public async Task RunRenovate()
    {
        var token = await GetGitHubToken(outputHelper);

        try
        {
            await ExecuteCommand(
                outputHelper,
                "npx",
                [
                    "renovate",
                    "workleap/renovate-config-test",
                    "--base-dir", this._repoPath,
                ],
                envVariables:
                [
                    new KeyValuePair<string, string>("LOG_LEVEL", "debug"),
                    new KeyValuePair<string, string>("RENOVATE_PRINT_CONFIG", "true"),
                    new KeyValuePair<string, string>("RENOVATE_TOKEN", token),
                    new KeyValuePair<string, string>("RENOVATE_BRANCH_PREFIX", new TemporaryRenovateBranchName(targetBranchName).Prefix),
                    new KeyValuePair<string, string>("RENOVATE_BASE_BRANCHES", targetBranchName.Name),
                    new KeyValuePair<string, string>("RENOVATE_USE_BASE_BRANCH_CONFIG", "merge"),
                    new KeyValuePair<string, string>("RENOVATE_RECREATE_WHEN", "always"),
                    new KeyValuePair<string, string>("RENOVATE_PR_HOURLY_LIMIT", "0"),
                    new KeyValuePair<string, string>("RENOVATE_PR_CONCURRENT_LIMIT", "0"),
                    new KeyValuePair<string, string>("RENOVATE_BRANCH_CONCURRENT_LIMIT", "0"),
                    new KeyValuePair<string, string>("RENOVATE_LABELS", "[\"renovate\"]"),
                    new KeyValuePair<string, string>("RENOVATE_INHERIT_CONFIG_FILE_NAME", "not-renovate.json"),
                    new KeyValuePair<string, string>("RENOVATE_REPOSITORIES", "[\"https://github.com/workleap/renovate-config-test\"]"),
                ]);
        }
        catch (Win32Exception ex) when (ex.Message.Contains("Target file or working directory doesn't exist"))
        {
            await ExecuteCommand(
                outputHelper,
                "docker",
                [
                    "run",
                    "--rm",
                    "-e", "LOG_LEVEL=debug",
                    "-e", "RENOVATE_PRINT_CONFIG=true",
                    "-e", $"RENOVATE_TOKEN={token}",
                    "-e", $"RENOVATE_BRANCH_PREFIX={new TemporaryRenovateBranchName(targetBranchName).Prefix}",
                    "-e", $"RENOVATE_BASE_BRANCHES={targetBranchName.Name}",
                    "-e", "RENOVATE_USE_BASE_BRANCH_CONFIG=merge",
                    "-e", "RENOVATE_RECREATE_WHEN=always",
                    "-e", "RENOVATE_PR_HOURLY_LIMIT=0",
                    "-e", "RENOVATE_PR_CONCURRENT_LIMIT=0",
                    "-e", "RENOVATE_BRANCH_CONCURRENT_LIMIT=0",
                    "-e", "RENOVATE_LABELS=[\"renovate\"]",
                    "-e", "RENOVATE_INHERIT_CONFIG_FILE_NAME=not-renovate.json",
                    "-e", "RENOVATE_REPOSITORIES=[\"https://github.com/workleap/renovate-config-test\"]",
                    "--pull", "always",
                    "renovate/renovate:latest",
                    "renovate",
                    "workleap/renovate-config-test"
                ]);
        }
    }

    [InlineSnapshotAssertion(nameof(expected))]
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument", Justification = "We want to forward the caller info")]
    public async Task AssertPullRequests(string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        var pullRequests = await this.GetPullRequests();

        /*
         * We scrub the line if there is a version number in it, so that we don't have to update the snapshot every time the version changes
         *
         * All the following would become `update dependency system.text.json to redacted`
         * update dependency system.text.json to 8.0.5 [security]
         * update dependency system.text.json to 8.0.5 [security] (#9834)
         * update dependency system.text.json to 8.0.5 (#2903)
         * update dependency system.text.json to 8.0.5
         */
        InlineSnapshot
            .WithSettings(settings => settings.ScrubLinesWithReplace(line => Regex.Replace(line, "(\\bto\\b).*", "to redacted")))
            .Validate(pullRequests, expected, filePath, lineNumber);
    }

    [InlineSnapshotAssertion(nameof(expected))]
    [SuppressMessage("ReSharper", "ExplicitCallerInfoArgument", Justification = "We want to forward the caller info")]
    public async Task AssertCommits(string? expected = null, [CallerFilePath] string? filePath = null, [CallerLineNumber] int lineNumber = -1)
    {
        var commits = await this.GetCommits();

        /*
         * We scrub the line if there is a version number in it, so that we don't have to update the snapshot every time the version changes
         *
         * All the following would become `update dependency system.text.json to redacted`
         * update dependency system.text.json to 8.0.5 [security]
         * update dependency system.text.json to 8.0.5 [security] (#9834)
         * update dependency system.text.json to 8.0.5 (#2903)
         * update dependency system.text.json (#2903)
         * update dependency system.text.json to 8.0.5
         */
        InlineSnapshot
            .WithSettings(settings => settings.ScrubLinesWithReplace(line => Regex.Replace(line, "(\\bto\\b).*|(\\([^)]*\\))", "to redacted")))
            .Validate(commits, expected, filePath, lineNumber);
    }

    private async Task<IEnumerable<PullRequestInfos>> GetPullRequests()
    {
        var pullRequests = await newGitHubClient.Repos[RepositoryOwner][RepositoryName].Pulls.GetAsync(x =>
        {
            x.QueryParameters.Base = targetBranchName.Name;
        }) ?? [];

        var pipeline = new MarkdownPipelineBuilder().UseAdvancedExtensions().Build();

        var pullRequestsInfos = new List<PullRequestInfos>(pullRequests.Count);

        foreach (var pullRequest in pullRequests.OrderBy(x => x.Title, StringComparer.Ordinal))
        {
            var markdownDocument = Markdown.Parse(pullRequest.Body!, pipeline);
            var prTitle = pullRequest.Title;
            var prLabels = pullRequest.Labels?.Select(x => x.Name).Order().ToArray();

            var table = markdownDocument.OfType<Table>().First();
            var rows = table.OfType<TableRow>().ToArray();
            var headers = rows[0].Select(cell => cell.InnerText()).ToArray();

            var packageUpdateInfos = new List<PackageUpdateInfos>(rows.Length);

            foreach (var row in rows.Skip(1))
            {
                var package = GetCell("Package")?.InnerText().Replace("( source )", string.Empty).Trim();
                var type = GetCell("Type")?.InnerText().Trim();
                var update = GetCell("Update")?.InnerText().Trim();

                packageUpdateInfos.Add(new PackageUpdateInfos(package, type, update));

                MarkdownObject? GetCell(string title)
                {
                    for (var i = 0; i < headers.Length; i++)
                    {
                        if (headers[i] == title)
                        {
                            return row[i];
                        }
                    }

                    return null;
                }
            }

            pullRequestsInfos.Add(new PullRequestInfos(
                prTitle!,
                prLabels!,
                packageUpdateInfos.OrderBy(x => x.Package).ThenBy(x => x.Type).ThenBy(x => x.Update),
                pullRequest.AutoMerge != null
                ));
        }

        return pullRequestsInfos;
    }

    private async Task<IEnumerable<CommitInfo>> GetCommits()
    {
        var commits = await newGitHubClient.Repos[RepositoryOwner][RepositoryName].Commits.GetAsync(x =>
        {
            x.QueryParameters.Sha = targetBranchName.Name;
        }) ?? [];

        return commits
            .Select(c =>
            {
                var message = c.CommitProp!.Message ?? string.Empty;

                return new CommitInfo(message);
            })
            .Where(c => !string.Equals(InitialCommitMessage, c.Message, StringComparison.Ordinal))
            .OrderBy(c => c.Message, StringComparer.Ordinal);
    }

    public async Task WaitForBranchPolicyChecksToSucceed()
    {
        var branches = await newGitHubClient.Repos[RepositoryOwner][RepositoryName].Branches.GetAsync() ?? [];

        foreach (var branch in branches)
        {
            var branchName = branch.Name!;
            if (TemporaryRenovateBranchName.TryParse(branchName, out var renovateBranchName) && renovateBranchName.Id == targetBranchName.Id)
            {
                await this.WaitForCommitAssociatedWorkflowsToSucceed(branch.Commit!.Sha!);
            }
        }
    }

    // Can't uses commit checks directly since fined grained permission token does not support checks scopes
    // Related issue: https://github.com/cli/cli/issues/8842
    private async Task WaitForCommitAssociatedWorkflowsToSucceed(string commitSha)
    {
        bool isCommitStatusCompleted;

        do
        {
            var workflows = await oldGitHubClient.Actions.Workflows.Runs.List(RepositoryOwner, RepositoryName,
                new WorkflowRunsRequest { HeadSha = commitSha });

            isCommitStatusCompleted =
                workflows.WorkflowRuns.Any() &&
                workflows.WorkflowRuns!.All(x => x.Status == WorkflowRunStatus.Completed);

            if (!isCommitStatusCompleted)
            {
                await Task.Delay(1000);
            }
        } while (!isCommitStatusCompleted);
    }

    private async Task CleanupRepository()
    {
        var branches = await newGitHubClient.Repos[RepositoryOwner][RepositoryName].Branches.GetAsync() ?? [];

        foreach (var branch in branches)
        {
            await this.CleanupBranch(branch.Name!);
        }
    }

    private async Task CleanupBranch(string branchName)
    {
        var isExpiredFeatureBranch = TemporaryFeatureBranchName.TryParse(branchName, out var tmpFeatureBranchName) && tmpFeatureBranchName.HasExpired();
        var isExpiredRenovateBranch = TemporaryRenovateBranchName.TryParse(branchName, out var tmpRenovateBranchName) && tmpRenovateBranchName.HasExpired();

        if (isExpiredFeatureBranch || isExpiredRenovateBranch)
        {
            outputHelper.WriteLine($"Deleting branch {branchName}");

            try
            {
                await newGitHubClient.Repos[RepositoryOwner][RepositoryName].Git.Refs[$"heads/{branchName}"].DeleteAsync();
            }
            catch (Exception)
            {
                // Ignore if it doesn't exist
                outputHelper.WriteLine($"Deleting branch was not found: {branchName}");
            }
        }
    }

    private static async Task<string> GetGitHubToken(ITestOutputHelper outputHelper)
    {
        var token = Environment.GetEnvironmentVariable("GH_TOKEN");
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

    private static async Task<OldGitHubClient> GetOldGitHubClient(ITestOutputHelper outputHelper)
    {
        if (_sharedOldGitHubClient != null)
        {
            return _sharedOldGitHubClient;
        }

        var token = await GetGitHubToken(outputHelper);
        _sharedOldGitHubClient = new OldGitHubClient(new ProductHeaderValue("renovate-test"), new InMemoryCredentialStore(new Octokit.Credentials(token)));

        return _sharedOldGitHubClient;
    }

    private static async Task<NewGitHubClient> GetNewGitHubClient(ITestOutputHelper outputHelper)
    {
        if (_sharedNewGitHubClient != null)
        {
            return _sharedNewGitHubClient;
        }

        var token = await GetGitHubToken(outputHelper);
        var tokenProvider = new TokenProvider(token);
        var adapter = RequestAdapter.Create(new TokenAuthProvider(tokenProvider));
        _sharedNewGitHubClient = new NewGitHubClient(adapter);

        return _sharedNewGitHubClient;
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

    public async ValueTask DisposeAsync()
    {
        await repositoryDirectory.DisposeAsync();
        await this.CleanupRepository();
    }
}