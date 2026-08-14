using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace Sunrise.Installer.Services;

public sealed class DepotDownloaderService(GitHubClient gitHub, InstallerLog log)
{
    private readonly string toolRoot = Path.Combine(AppConstants.AppDataRoot, "tools", "DepotDownloader");

    public async Task<string> EnsureAvailableAsync(
        IProgress<int>? progress,
        CancellationToken cancellationToken)
    {
        ReleaseInfo release = await gitHub.GetLatestReleaseAsync(
            AppConstants.DepotDownloaderOwner,
            AppConstants.DepotDownloaderRepository,
            GitHubClient.SelectDepotDownloaderAsset,
            cancellationToken);
        string versionName = SanitizeFileName(release.Tag);
        string versionDirectory = Path.Combine(toolRoot, "versions", versionName);
        string executable = Path.Combine(versionDirectory, "DepotDownloader.exe");
        if (File.Exists(executable))
        {
            return executable;
        }

        Directory.CreateDirectory(Path.Combine(toolRoot, "versions"));
        string temporaryDirectory = Path.Combine(toolRoot, "versions", $".{versionName}-{Guid.NewGuid():N}");
        string archivePath = Path.Combine(temporaryDirectory, "DepotDownloader.zip");
        Directory.CreateDirectory(temporaryDirectory);

        try
        {
            log.Info("tool_download", "Downloading DepotDownloader.", ("tag", release.Tag));
            await gitHub.DownloadAsync(release.Asset, archivePath, progress, cancellationToken);
            string extracted = Path.Combine(temporaryDirectory, "extracted");
            await SafeZip.ExtractAllAsync(archivePath, extracted, cancellationToken);
            string extractedExecutable = Directory
                .EnumerateFiles(extracted, "DepotDownloader.exe", SearchOption.AllDirectories)
                .SingleOrDefault()
                ?? throw new InstallerException("The DepotDownloader ZIP does not contain DepotDownloader.exe.");

            string extractedRoot = Path.GetDirectoryName(extractedExecutable)!;
            try
            {
                Directory.Move(extractedRoot, versionDirectory);
            }
            catch (IOException) when (File.Exists(executable))
            {
            }

            if (!File.Exists(executable))
            {
                throw new InstallerException("DepotDownloader could not be installed.");
            }

            log.Info("tool_ready", "DepotDownloader is ready.", ("tag", release.Tag));
            return executable;
        }
        finally
        {
            FileCleanup.TryDeleteDirectory(temporaryDirectory);
        }
    }

    public async Task DownloadDepotsAsync(
        string executable,
        string installRoot,
        string steamUsername,
        IReadOnlyList<DepotSpec> depots,
        bool validate,
        IProgress<string>? status,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(toolRoot);
        using ConsoleWindow console = new("Sunrise Installer - Steam sign-in");
        Console.WriteLine("Sunrise Installer");
        Console.WriteLine("DepotDownloader needs a Steam account that owns the game.");
        Console.WriteLine("Credentials are handled by DepotDownloader in this window.");
        Console.WriteLine();

        foreach (DepotSpec depot in depots)
        {
            status?.Report($"{(validate ? "Validating" : "Downloading")} depot {depot.DepotId}...");
            int exitCode = await RunAsync(
                executable,
                installRoot,
                steamUsername,
                depot,
                validate,
                cancellationToken);
            if (exitCode != 0)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"DepotDownloader stopped with exit code {exitCode}.");
                throw new InstallerException(DepotFailureMessage(exitCode));
            }
        }

        Console.WriteLine();
        Console.WriteLine("Steam files are ready. Return to the Sunrise Installer.");
    }

    private async Task<int> RunAsync(
        string executable,
        string installRoot,
        string steamUsername,
        DepotSpec depot,
        bool validate,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = executable,
            WorkingDirectory = toolRoot,
            UseShellExecute = false,
            CreateNoWindow = false,
        };
        AddArgument(startInfo, "-app", AppConstants.SteamAppId.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "-depot", depot.DepotId.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "-manifest", depot.ManifestId.ToString(CultureInfo.InvariantCulture));
        AddArgument(startInfo, "-dir", installRoot);
        AddArgument(startInfo, "-username", steamUsername);
        startInfo.ArgumentList.Add("-remember-password");
        AddArgument(startInfo, "-os", "windows");
        AddArgument(startInfo, "-osarch", "64");
        if (validate)
        {
            startInfo.ArgumentList.Add("-validate");
        }

        log.Info(
            "depot_start",
            $"Starting depot {depot.DepotId}.",
            ("depot", depot.DepotId),
            ("manifest", depot.ManifestId),
            ("validate", validate));
        using Process process = new() { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InstallerException("DepotDownloader could not be started.");
            }

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill(entireProcessTree: true);
                    }
                }
                catch
                {
                }
            });
            await process.WaitForExitAsync(cancellationToken);
            log.Info(
                "depot_done",
                $"Depot {depot.DepotId} finished.",
                ("depot", depot.DepotId),
                ("code", process.ExitCode));
            return process.ExitCode;
        }
        catch (System.ComponentModel.Win32Exception exception)
        {
            throw new InstallerException("DepotDownloader could not start. Security software may have blocked it.", exception);
        }
    }

    private static void AddArgument(ProcessStartInfo startInfo, string name, string value)
    {
        startInfo.ArgumentList.Add(name);
        startInfo.ArgumentList.Add(value);
    }

    private static string DepotFailureMessage(int exitCode) =>
        $"DepotDownloader failed with exit code {exitCode}. " +
        "Read the Steam window for the exact error. Check ownership, Steam Guard, free space, and the install folder.";

    private static string SanitizeFileName(string value)
    {
        StringBuilder result = new(value.Length);
        foreach (char character in value)
        {
            result.Append(Path.GetInvalidFileNameChars().Contains(character) ? '_' : character);
        }

        return result.ToString();
    }

}
