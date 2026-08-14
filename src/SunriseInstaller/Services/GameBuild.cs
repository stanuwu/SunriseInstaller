using System.Diagnostics;
using System.Globalization;

namespace Sunrise.Installer.Services;

/// <summary>
/// Turns the version stamp of destiny2.exe into something readable. Bungie stamps builds as
/// "[build].[yy].[mm].[dd].[hhmm].[branch]", for example "86657.20.08.23.1800.d2_rc", so the stamp
/// already carries the date the build was compiled and no manifest lookup is needed to name it.
/// </summary>
public static class GameBuild
{
    /// <summary>
    /// Seasons and the dates they ran between. A build is stamped when it was compiled rather than when
    /// it shipped, so a build made in the last days of a season is named after that season, not the next.
    /// </summary>
    private static readonly Season[] Seasons =
    [
        new("Season of Opulence", new DateOnly(2019, 6, 4), new DateOnly(2019, 10, 1)),
        new("Season of the Undying", new DateOnly(2019, 10, 1), new DateOnly(2019, 12, 10)),
        new("Season of Dawn", new DateOnly(2019, 12, 10), new DateOnly(2020, 3, 10)),
        new("Season of the Worthy", new DateOnly(2020, 3, 10), new DateOnly(2020, 6, 9)),
        new("Season of Arrivals", new DateOnly(2020, 6, 9), new DateOnly(2020, 11, 10)),
        new("Season of the Hunt", new DateOnly(2020, 11, 10), new DateOnly(2021, 2, 9)),
    ];

    /// <summary>Describes the game installed in a folder, or null when no game is installed there.</summary>
    public static string? Read(string installRoot)
    {
        string executable = Path.Combine(installRoot, AppConstants.GameExecutableName);
        if (!File.Exists(executable))
        {
            return null;
        }

        try
        {
            string? version = FileVersionInfo.GetVersionInfo(executable).FileVersion?.Trim();
            return string.IsNullOrEmpty(version) ? null : Describe(version);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
    }

    /// <summary>Names a raw version stamp, falling back to the stamp itself when it cannot be read.</summary>
    public static string Describe(string version)
    {
        if (!TryParse(version, out int build, out DateOnly date))
        {
            return version;
        }

        string stamp = $"build {build} ({date.ToString("d MMM yyyy", CultureInfo.InvariantCulture)})";
        string? season = SeasonFor(date);
        return season is null ? stamp : $"{season}, {stamp}";
    }

    private static string? SeasonFor(DateOnly date) =>
        Array.Find(Seasons, season => date >= season.Start && date < season.End)?.Name;

    private static bool TryParse(string version, out int build, out DateOnly date)
    {
        build = 0;
        date = default;
        string[] parts = version.Split('.');
        if (parts.Length < 5)
        {
            return false;
        }

        if (!TryReadNumber(parts[0], out build) ||
            !TryReadNumber(parts[1], out int year) ||
            !TryReadNumber(parts[2], out int month) ||
            !TryReadNumber(parts[3], out int day))
        {
            return false;
        }

        if (year > 99 || month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(2000 + year, month))
        {
            return false;
        }

        date = new DateOnly(2000 + year, month, day);
        return true;
    }

    private static bool TryReadNumber(string text, out int value) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
}
