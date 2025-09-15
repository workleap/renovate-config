using Xunit.Abstractions;

namespace renovate_config.tests;

public class PrivateAdoFeedsTests(ITestOutputHelper testOutputHelper)
{
[Fact]
    public async Task Given_Various_Updates_From_ADO_Feeds_When_Renovate_Then_Create_Prs()
    {
        await using var testContext = await TestContext.CreateAsync(testOutputHelper);

        testContext.AddSuccessfulWorkflowFileToSatisfyBranchPolicy();

        testContext.AddFile(
            "Directory.Packages.props", /*lang=xml*/
            """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
                <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
              </PropertyGroup>
              <ItemGroup>
                <PackageVersion Include="Workleap.DotNet.CodingStandards" Version="1.1.14" />
              </ItemGroup>
              <ItemGroup>
                <PackageVersion Include="ShareGate.Crawler.DomainEvents" Version="1.1.13" />
                <PackageVersion Include="Workleap.Configuration.ConfigurationStore" Version="2.2.219" />
              </ItemGroup>
            </Project>
            """);

        await testContext.PushFilesOnTemporaryBranch();
        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: chore(deps): update dependency workleap.dotnet.codingstandards to redacted
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: Workleap.DotNet.CodingStandards
            """);
    }
}