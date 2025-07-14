using Xunit.Abstractions;

namespace renovate_config.tests;

public sealed class SystemTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public async Task Given_Folder_Specific_AutoMerge_Config_Then_Only_AutoMerges_Matching_Folder()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);

        // Add renovate config that enables automerge only for src/auto
        // testContext.UseRenovateFile("all-automerge.json");
        testContext.AddFile("renovate.json", /*lang=json*/
            """
            {
                "$schema": "https://docs.renovatebot.com/renovate-schema.json",
                "extends": [
                  "github>workleap/renovate-config"
                ],
                "packageRules": [
                    {
                      "matchPaths": ["src/auto/**"],
                      "matchUpdateTypes": ["minor", "patch"],
                      "extends": [
                         ":automergeBranch",
                         ":automergeMinor"
                      ]
                    }
                ]
            }
            """);

        // Add updatable dependencies in both folders
        testContext.AddFile("src/auto/project.csproj", /*lang=xml*/
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="System.Text.Json" Version="8.0.0" />
              </ItemGroup>
            </Project>
            """);

        testContext.AddFile(
            "src/manual/roject.csproj", /*lang=xml*/
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Workleap.Extensions.Mongo" Version="1.11.0" />
              </ItemGroup>
            </Project>
            """);

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();

        await testContext.PushFilesOnTemporaryBranch();
        await testContext.RunRenovate();
        await testContext.WaitForBranchPolicyChecksToSucceed();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: fix(deps): update dependency left-pad to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: left-pad
                  Type: dependencies
                  Update: minor
              IsAutoMergeEnabled: true
            - Title: fix(deps): update dependency left-pad to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: left-pad
                  Type: dependencies
                  Update: minor
            """);

        await testContext.AssertCommits("");
    }
}