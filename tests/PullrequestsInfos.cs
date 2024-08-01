namespace renovate_config.tests;

public sealed record PullRequestInfos(string Title, IEnumerable<string> Labels, IEnumerable<PackageUpdateInfos> PackageUpdatesInfos);

public sealed record PackageUpdateInfos(string Package, string Type, string Update);