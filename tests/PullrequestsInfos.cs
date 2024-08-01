namespace renovate_config.tests;

internal sealed record PullRequestInfos(string Title, IEnumerable<string> Labels, IEnumerable<PackageUpdateInfos> PackageUpdatesInfos);

internal sealed record PackageUpdateInfos(string Package, string Type, string Update);