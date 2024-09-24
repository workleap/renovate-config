using System.Diagnostics.CodeAnalysis;

namespace renovate_config.tests;

[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1649:File name should match first type name", Justification = "Keeping git DTOs together")]
internal sealed record PullRequestInfos(string Title, IEnumerable<string> Labels, IEnumerable<PackageUpdateInfos> PackageUpdatesInfos, bool IsAutoMergeEnabled);

internal sealed record PackageUpdateInfos(string Package, string Type, string Update);

internal sealed record CommitInfo(string Message);

