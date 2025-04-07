using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace renovate_config.tests;

internal sealed class TemporaryRenovateBranchName : TemporaryBranchName
{
    private const string FolderName = "renovate";

    public TemporaryRenovateBranchName(TemporaryFeatureBranchName fromBranchName)
        : this(fromBranchName.Date, fromBranchName.Id)
    {
    }

    private TemporaryRenovateBranchName(DateTimeOffset date, string id)
    {
        this.Date = date;
        this.Id = id;
        this.Prefix = $"{FolderName}{Slash}{date.ToString(DateFormat, CultureInfo.InvariantCulture)}{Underscore}{id}{Underscore}";
    }

    public override DateTimeOffset Date { get; }

    public override string Id { get; }

    public string Prefix { get; }

    public static bool TryParse(string branchName, [NotNullWhen(true)] out TemporaryRenovateBranchName? result)
    {
        if (branchName.Split(Slash, Underscore) is not [FolderName, { } datePart, { } idPart, ..])
        {
            result = null;
            return false;
        }

        if (!DateTimeOffset.TryParseExact(datePart, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var date))
        {
            result = null;
            return false;
        }

        result = new TemporaryRenovateBranchName(date, idPart);
        return true;
    }
}