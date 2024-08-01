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

        testContext.AddFile("global.json", /*lang=json*/"""{"sdk": {"version": "6.0.100"}}""");
        testContext.AddFile("Dockerfile",
            """
            FROM mcr.microsoft.com/dotnet/sdk:6.0.100
            FROM mcr.microsoft.com/dotnet/aspnet:6.0.0
            """);

        await testContext.RunRenovate();

        await testContext.AssertPullRequests(
            """
            - Title: chore(deps): update dotnet-sdk
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: dotnet-sdk
                  Type: dotnet-sdk
                  Update: patch
                - Package: mcr.microsoft.com/dotnet/aspnet
                  Type: final
                  Update: patch
                - Package: mcr.microsoft.com/dotnet/sdk
                  Type: stage
                  Update: patch
            - Title: chore(deps): update dotnet-sdk  to redacted(major)
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: dotnet-sdk
                  Type: dotnet-sdk
                  Update: major
                - Package: mcr.microsoft.com/dotnet/aspnet
                  Type: final
                  Update: major
                - Package: mcr.microsoft.com/dotnet/sdk
                  Type: stage
                  Update: major
            """);
    }

    [Fact]
    public async Task RenovateHangfireDependencies()
    {
      var testContext = await TestContext.CreateAsync(_testOutputHelper);

      testContext.AddFile("project.csproj",
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="Hangfire" Version="1.7.0" />
            <PackageReference Include="Hangfire.Core" Version="1.7.0" />
            <PackageReference Include="Hangfire.NetCore" Version="1.7.0" />
            <PackageReference Include="Hangfire.Pro" Version="2.3.2" />
            <PackageReference Include="Hangfire.Pro.Redis" Version="2.3.2" />
            <PackageReference Include="Hangfire.Throttling" Version="1.1.1" />
          </ItemGroup>
        </Project>
        """);

      await testContext.RunRenovate();

      await testContext.AssertPullRequests(
        """
        - Title: chore(deps): update dependency hangfire  to redacted
          Labels:
            - renovate
          PackageUpdatesInfos:
            - Package: Hangfire  ( source )
              Type: nuget
              Update: minor
        - Title: chore(deps): update hangfire
          Labels:
            - renovate
          PackageUpdatesInfos:
            - Package: Hangfire.Core  ( source )
              Type: nuget
              Update: minor
            - Package: Hangfire.NetCore  ( source )
              Type: nuget
              Update: minor
            - Package: Hangfire.Pro.Redis
              Type: nuget
              Update: minor
        """);
    }

    [Fact]
    public async Task RenovatePackageJsonDependencies()
    {
      var testContext = await TestContext.CreateAsync(_testOutputHelper);

      testContext.AddFile("package.json",
        """
        {
          "dependencies": {
            "@azure/msal-browser": "^3.13.0",
            "@azure/msal-react": "~2.0.15"
          },
          "devDependencies": {
            "@storybook/addon-essentials": "^8.0.10",
            "@storybook/addon-interactions": "~8.0.10"
          }
        }
        """);

      await testContext.RunRenovate();

      await testContext.AssertPullRequests(
          """
            - Title: fix(deps): pin dependencies
              Labels:
                - renovate
              PackageUpdatesInfos:
                - Package: @azure/msal-browser
                  Type: dependencies
                  Update: pin
                - Package: @azure/msal-react
                  Type: dependencies
                  Update: pin
                - Package: @storybook/addon-essentials  ( source )
                  Type: devDependencies
                  Update: pin
                - Package: @storybook/addon-interactions  ( source )
                  Type: devDependencies
                  Update: pin
            """);
    }

    [Fact]
    public async Task RenovateMicrosoftDependencies()
    {
      var testContext = await TestContext.CreateAsync(_testOutputHelper);

      testContext.AddFile("project.csproj",
        """
        <Project Sdk="Microsoft.NET.Sdk">
          <ItemGroup>
            <PackageReference Include="System.Text.Json" Version="7.0.0" />
            <PackageReference Include="Microsoft.ApplicationInsights.AspNetCore" Version="1.0.2" />
            <PackageReference Include="microsoft.AspNetCore.Authentication.OpenIdConnect" Version="7.0.0" />
            <PackageReference Include="Microsoft.Azure.AppConfiguration.AspNetCore" Version="7.0.0" />
            <PackageReference Include="Microsoft.CodeAnalysis.PublicApiAnalyzers" Version="3.3.4">
              <PrivateAssets>all</PrivateAssets>
              <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
            </PackageReference>
          </ItemGroup>
        </Project>
        """);

      await testContext.RunRenovate();

      await testContext.AssertPullRequests(
          """
        - Title: chore(deps): update microsoft
          Labels:
            - renovate
          PackageUpdatesInfos:
            - Package: microsoft.AspNetCore.Authentication.OpenIdConnect  ( source )
              Type: nuget
              Update: patch
            - Package: Microsoft.Azure.AppConfiguration.AspNetCore  ( source )
              Type: nuget
              Update: minor
            - Package: System.Text.Json  ( source )
              Type: nuget
              Update: patch
        - Title: chore(deps): update microsoft (major)
          Labels:
            - renovate
          PackageUpdatesInfos:
            - Package: Microsoft.ApplicationInsights.AspNetCore  ( source )
              Type: nuget
              Update: major
            - Package: microsoft.AspNetCore.Authentication.OpenIdConnect  ( source )
              Type: nuget
              Update: major
            - Package: System.Text.Json  ( source )
              Type: nuget
              Update: major
        """);
    }
}

public record PullRequestInfos(string Title, IEnumerable<string> Labels, IEnumerable<PackageUpdateInfos> PackageUpdatesInfos);

public record PackageUpdateInfos(string Package, string Type, string Update);
