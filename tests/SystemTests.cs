using Markdig.Renderers.Normalize;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;
using Meziantou.Framework.InlineSnapshotTesting;
using Xunit.Abstractions;

namespace renovate_config.tests;

public class SystemTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    public SystemTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task RenovateDotnetSdkDependencies()
    {

        var testContext = await TestContext.CreateAsync(_testOutputHelper);

        testContext.AddFile("global.json", /*lang=json*/"""{"sdk": {"version": "5.0.100"}}""");

        await testContext.RunRenovate();

        var pullRequests = await testContext.GetPullRequests();

        InlineSnapshot.Validate(pullRequests, """
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