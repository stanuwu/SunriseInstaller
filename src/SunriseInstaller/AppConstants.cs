namespace Sunrise.Installer;

public static class AppConstants
{
    public const uint SteamAppId = 1085660;
    public const string GameExecutableName = "destiny2.exe";
    public const string ModRelativePath = @"bin\x64\steam_api64.dll";
    public const string SunriseOwner = "stanuwu";
    public const string SunriseRepository = "Sunrise";
    public const string DepotDownloaderOwner = "SteamRE";
    public const string DepotDownloaderRepository = "DepotDownloader";
    public const long FreshInstallFreeBytes = 110L * 1024 * 1024 * 1024;
    public const long RepairFreeBytes = 5L * 1024 * 1024 * 1024;
    public const long UpdateFreeBytes = 256L * 1024 * 1024;

    /// <summary>
    /// The depots that hold the game. A game version only changes the manifest each depot is pinned to.
    /// </summary>
    public static readonly uint[] DepotIds = [1085661, 1085662];

    /// <summary>
    /// Game versions that Sunrise is known to work with. Manifest ids come from the depot history on
    /// steamdb.info; add an entry here once a build has been tested with the mod.
    /// </summary>
    public static readonly GameVersion[] KnownVersions =
    [
        new(
            "Season of Arrivals (build 86657, 23 Aug 2020)",
            [new DepotSpec(1085661, 7180122903232116872), new DepotSpec(1085662, 2210332166360342287)]),
    ];

    public static GameVersion DefaultVersion => KnownVersions[0];

    public static string AppDataRoot =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunriseInstaller");
}
