namespace renovate_config.tests;

internal abstract class TemporaryBranchName
{
    public const string DateFormat = "yyyyMMddHHmmss";
    public static readonly TimeSpan MaximumAge = TimeSpan.FromHours(2);
    protected const char Slash = '/';
    protected const char Underscore = '_';

    public abstract DateTimeOffset Date { get; }

    public abstract string Id { get; }

    public bool HasExpired()
    {
        return this.HasExpired(DateTimeOffset.UtcNow);
    }

    public bool HasExpired(DateTimeOffset utcNow)
    {
        return this.Date <= utcNow.Add(MaximumAge.Negate());
    }
}