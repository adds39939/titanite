namespace Titanite.Abstractions.Updates;

public sealed record AppUpdate(string Version, string BundleName, Uri BundleUrl);
