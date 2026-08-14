namespace Sunrise.Installer;

public sealed record DepotSpec(uint DepotId, ulong ManifestId);

/// <summary>A named set of depot manifests, in other words one downloadable version of the game.</summary>
public sealed record GameVersion(string Name, IReadOnlyList<DepotSpec> Depots);

/// <summary>A Destiny 2 season and the dates it ran between, used to name a build.</summary>
public sealed record Season(string Name, DateOnly Start, DateOnly End);

public sealed record ReleaseAsset(
    string Name,
    Uri DownloadUrl,
    long Size,
    string? Digest);

public sealed record ReleaseInfo(
    string Tag,
    ReleaseAsset Asset);

public sealed record PreparedPayload(
    ReleaseInfo Release,
    string StagingDirectory,
    string DllPath,
    string DllSha256);

public sealed class InstallerState
{
    public int SchemaVersion { get; set; } = 1;
    public uint AppId { get; set; } = AppConstants.SteamAppId;
    public string ReleaseTag { get; set; } = string.Empty;
    public string ReleaseAsset { get; set; } = string.Empty;
    public string? ReleaseAssetDigest { get; set; }
    public string InstalledDllSha256 { get; set; } = string.Empty;
    public DateTimeOffset InstalledAtUtc { get; set; }
    public Dictionary<uint, ulong> Manifests { get; set; } = [];
}

public sealed class UserPreferences
{
    public string InstallDirectory { get; set; } = string.Empty;
    public string SteamUsername { get; set; } = string.Empty;
    public string GameVersion { get; set; } = string.Empty;
    public Dictionary<uint, ulong> CustomManifests { get; set; } = [];
}

public enum UpdateStatus
{
    Current,
    ReleaseAvailable,
    LocalFileChanged,
    NotInstalled,
}

public sealed record UpdateCheck(UpdateStatus Status, string Message, ReleaseInfo LatestRelease);

public sealed class InstallerException(string message, Exception? innerException = null)
    : Exception(message, innerException);
