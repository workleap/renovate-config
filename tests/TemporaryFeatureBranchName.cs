using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace renovate_config.tests;

internal sealed class TemporaryFeatureBranchName : TemporaryBranchName
{
    private const string FolderName = "feature";

    private static int _branchCounter;
    private static readonly object BranchCounterLock = new();

    public static TemporaryFeatureBranchName Create()
    {
        lock (BranchCounterLock)
        {
            _branchCounter++;
            return new TemporaryFeatureBranchName(DateTimeOffset.UtcNow, _branchCounter.ToString());
        }
    }

    private TemporaryFeatureBranchName(DateTimeOffset date, string id)
    {
        this.Date = date;
        this.Id = id;
        this.Name = $"{FolderName}{Slash}{date.ToString(DateFormat, CultureInfo.InvariantCulture)}{Underscore}{id}";
    }

    public override DateTimeOffset Date { get; }

    public override string Id { get; }

    public string Name { get; }

    public static bool TryParse(string branchName, [NotNullWhen(true)] out TemporaryFeatureBranchName? result)
    {
        if (branchName.Split(Slash, Underscore) is not [FolderName, { } datePart, { } idPart])
        {
            result = null;
            return false;
        }

        if (!DateTimeOffset.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            result = null;
            return false;
        }

        result = new TemporaryFeatureBranchName(date, idPart);
        return true;
    }

    public override string ToString()
    {
        return this.Name;
    }
}