using System.Globalization;

namespace renovate_config.tests;

public sealed class TemporaryBranchNameTests
{
    [Theory]
    [InlineData("main")]
    [InlineData("renovate/major-microsoft")]
    [InlineData("feature/20250403200016_dab5d7514d194cdcbdfac98939b18b3d")]
    [InlineData("feature/20250403200016_dab5d7514d194cdcbdfac98939b18b3d_")]
    public void TemporaryRenovateBranchName_ParseFail(string branchName)
    {
        Assert.False(TemporaryRenovateBranchName.TryParse(branchName, out _));
    }

    [Fact]
    public void TemporaryRenovateBranchName_HappyPath()
    {
        const string branchName = "renovate/20250403200016_dab5d7514d194cdcbdfac98939b18b3d_major-microsoft";
        var expectedDate = ParseDateTimeOffsetUtc("20250403200016");

        Assert.True(TemporaryRenovateBranchName.TryParse(branchName, out var result));

        Assert.Equal(expectedDate, result.Date);
        Assert.Equal("dab5d7514d194cdcbdfac98939b18b3d", result.Id);
        Assert.Equal("renovate/20250403200016_dab5d7514d194cdcbdfac98939b18b3d_", result.Prefix);

        Assert.False(result.HasExpired(expectedDate));
        Assert.True(result.HasExpired(expectedDate + TemporaryBranchName.MaximumAge));
        Assert.False(result.HasExpired(expectedDate + TemporaryBranchName.MaximumAge + TimeSpan.FromTicks(-1)));
    }

    [Theory]
    [InlineData("main")]
    [InlineData("renovate/major-microsoft")]
    [InlineData("feature/something")]
    [InlineData("feature/20250403200016")]
    [InlineData("feature/20250403200016-dab5d7514d194cdcbdfac98939b18b3d")]
    [InlineData("feature/20250403200016_dab5d7514d194cdcbdfac98939b18b3d_")]
    public void TemporaryFeatureBranchName_ParseFail(string branchName)
    {
        Assert.False(TemporaryFeatureBranchName.TryParse(branchName, out _));
    }

    [Fact]
    public void TemporaryFeatureBranchName_HappyPath()
    {
        const string branchName = "feature/20250403200016_dab5d7514d194cdcbdfac98939b18b3d";
        var expectedDate = ParseDateTimeOffsetUtc("20250403200016");

        Assert.True(TemporaryFeatureBranchName.TryParse(branchName, out var result));

        Assert.Equal(expectedDate, result.Date);
        Assert.Equal("dab5d7514d194cdcbdfac98939b18b3d", result.Id);
        Assert.Equal("feature/20250403200016_dab5d7514d194cdcbdfac98939b18b3d", result.Name);

        Assert.False(result.HasExpired(expectedDate));
        Assert.True(result.HasExpired(expectedDate + TemporaryBranchName.MaximumAge));
        Assert.False(result.HasExpired(expectedDate + TemporaryBranchName.MaximumAge + TimeSpan.FromTicks(-1)));
    }

    private static DateTimeOffset ParseDateTimeOffsetUtc(string text)
    {
        return DateTimeOffset.ParseExact(text, TemporaryBranchName.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
    }
}