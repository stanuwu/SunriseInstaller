namespace Sunrise.Installer.Services;

public sealed class InstallCoordinator : IDisposable
{
    private readonly InstallerLog log;
    private readonly GitHubClient gitHub;
    private readonly SunriseReleaseService sunrise;
    private readonly DepotDownloaderService depots;
    private readonly PayloadInstaller payloadInstaller;
    private readonly JsonStores stores;

    public InstallCoordinator(InstallerLog log, AppOptions options)
    {
        this.log = log;
        gitHub = new GitHubClient(log);
        sunrise = new SunriseReleaseService(gitHub, log, options.TestPayloadPath);
        depots = new DepotDownloaderService(gitHub, log);
        payloadInstaller = new PayloadInstaller(log);
        stores = new JsonStores(log);
    }

    public async Task InstallAsync(
        string installRoot,
        string steamUsername,
        IReadOnlyList<DepotSpec> depots,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateSteamUsername(steamUsername);
        string root = Preflight.PrepareInstallRoot(installRoot, AppConstants.FreshInstallFreeBytes);
        Preflight.EnsureGameIsClosed();
        log.Info("install_start", "Install started.", ("mode", "install"));

        PreparedPayload? payload = null;
        try
        {
            await PrepareGameFilesAsync(
                root,
                steamUsername.Trim(),
                depots,
                validate: false,
                progress,
                cancellationToken);
            progress?.Report(new OperationProgress("Preparing Sunrise...", 92));
            payload = await sunrise.PrepareLatestAsync(root, ScaleProgress(progress, 92, 96), cancellationToken);
            progress?.Report(new OperationProgress("Installing Sunrise...", 97));
            await payloadInstaller.ApplyAsync(root, payload, preserveDepotDll: true, cancellationToken);
            await SaveStateAsync(root, payload, depots, cancellationToken);
            progress?.Report(new OperationProgress("Install complete.", 100));
            log.Info("install_done", "Install complete.", ("mode", "install"));
        }
        finally
        {
            if (payload is not null)
            {
                SunriseReleaseService.Cleanup(payload);
            }
        }
    }

    public async Task RepairAsync(
        string installRoot,
        string steamUsername,
        IReadOnlyList<DepotSpec> depots,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ValidateSteamUsername(steamUsername);
        string root = Preflight.PrepareInstallRoot(installRoot, AppConstants.RepairFreeBytes);
        Preflight.EnsureGameIsClosed();
        if (!File.Exists(Path.Combine(root, AppConstants.GameExecutableName)))
        {
            throw new InstallerException("No Destiny 2 install was found in this folder. Use Install first.");
        }

        log.Info("repair_start", "Repair started.", ("mode", "repair"));
        IReadOnlyList<DepotSpec> installed = await InstalledDepotsAsync(root, depots, cancellationToken);
        PreparedPayload? payload = null;
        try
        {
            await PrepareGameFilesAsync(
                root,
                steamUsername.Trim(),
                installed,
                validate: true,
                progress,
                cancellationToken);
            progress?.Report(new OperationProgress("Deleting Sunrise config and cached data...", 95));
            payloadInstaller.DeleteSunriseData(root);
            progress?.Report(new OperationProgress("Preparing Sunrise...", 96));
            payload = await sunrise.PrepareLatestAsync(root, ScaleProgress(progress, 96, 98), cancellationToken);
            progress?.Report(new OperationProgress("Reinstalling Sunrise...", 99));
            await payloadInstaller.ApplyAsync(root, payload, preserveDepotDll: true, cancellationToken);
            await SaveStateAsync(root, payload, installed, cancellationToken);
            progress?.Report(new OperationProgress("Repair complete.", 100));
            log.Info("repair_done", "Repair complete.", ("mode", "repair"));
        }
        finally
        {
            if (payload is not null)
            {
                SunriseReleaseService.Cleanup(payload);
            }
        }
    }

    public async Task<bool> UpdateAsync(
        string installRoot,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        string root = Preflight.PrepareInstallRoot(installRoot, AppConstants.UpdateFreeBytes);
        Preflight.EnsureGameIsClosed();
        VerifyGameFiles(root);
        progress?.Report(new OperationProgress("Checking for updates...", 5));
        UpdateCheck check = await CheckForUpdateAsync(root, cancellationToken);
        if (check.Status == UpdateStatus.Current)
        {
            progress?.Report(new OperationProgress(check.Message, 100));
            return false;
        }

        log.Info("update_start", "Update started.", ("tag", check.LatestRelease.Tag));
        IReadOnlyList<DepotSpec> installed = await InstalledDepotsAsync(root, [], cancellationToken);
        PreparedPayload? payload = null;
        try
        {
            payload = await sunrise.PrepareLatestAsync(root, ScaleProgress(progress, 10, 85), cancellationToken);
            progress?.Report(new OperationProgress("Installing the update...", 90));
            await payloadInstaller.ApplyAsync(root, payload, preserveDepotDll: false, cancellationToken);
            await SaveStateAsync(root, payload, installed, cancellationToken);
            progress?.Report(new OperationProgress($"Updated to {payload.Release.Tag}.", 100));
            log.Info("update_done", "Update complete.", ("tag", payload.Release.Tag));
            return true;
        }
        finally
        {
            if (payload is not null)
            {
                SunriseReleaseService.Cleanup(payload);
            }
        }
    }

    public async Task<UpdateCheck> CheckForUpdateAsync(
        string installRoot,
        CancellationToken cancellationToken)
    {
        ReleaseInfo latest = await sunrise.GetLatestAsync(cancellationToken);
        InstallerState? state = await stores.LoadStateAsync(installRoot, cancellationToken);
        if (state is null)
        {
            return new UpdateCheck(UpdateStatus.NotInstalled, "Sunrise is not installed by this installer.", latest);
        }

        if (!state.ReleaseTag.Equals(latest.Tag, StringComparison.OrdinalIgnoreCase) ||
            DigestsDiffer(state.ReleaseAssetDigest, latest.Asset.Digest))
        {
            return new UpdateCheck(
                UpdateStatus.ReleaseAvailable,
                $"Sunrise {latest.Tag} is available. Installed: {state.ReleaseTag}.",
                latest);
        }

        string dllPath = Path.Combine(installRoot, AppConstants.ModRelativePath);
        if (!File.Exists(dllPath))
        {
            return new UpdateCheck(UpdateStatus.LocalFileChanged, "The Sunrise DLL is missing.", latest);
        }

        string localHash = await FileIntegrity.Sha256Async(dllPath, cancellationToken);
        if (!localHash.Equals(state.InstalledDllSha256, StringComparison.OrdinalIgnoreCase))
        {
            return new UpdateCheck(UpdateStatus.LocalFileChanged, "The installed Sunrise DLL has changed.", latest);
        }

        return new UpdateCheck(UpdateStatus.Current, $"Sunrise {state.ReleaseTag} is current.", latest);
    }

    public Task<InstallerState?> LoadStateAsync(string installRoot, CancellationToken cancellationToken) =>
        stores.LoadStateAsync(installRoot, cancellationToken);

    public static Task<UserPreferences> LoadPreferencesAsync(CancellationToken cancellationToken) =>
        JsonStores.LoadPreferencesAsync(cancellationToken);

    public static Task SavePreferencesAsync(UserPreferences preferences, CancellationToken cancellationToken) =>
        JsonStores.SavePreferencesAsync(preferences, cancellationToken);

    public void Dispose() => gitHub.Dispose();

    private async Task PrepareGameFilesAsync(
        string installRoot,
        string steamUsername,
        IReadOnlyList<DepotSpec> gameDepots,
        bool validate,
        IProgress<OperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        progress?.Report(new OperationProgress("Preparing DepotDownloader...", 2));
        string downloader = await depots.EnsureAvailableAsync(
            ScaleProgress(progress, 2, 8),
            cancellationToken);
        await depots.DownloadDepotsAsync(
            downloader,
            installRoot,
            steamUsername,
            gameDepots,
            validate,
            MessageProgress(progress),
            cancellationToken);
        VerifyGameFiles(installRoot);
    }

    /// <summary>
    /// The manifests the folder was installed with. Repair and Update must reuse them instead of the
    /// selected version, otherwise repairing an install would quietly convert it to another version.
    /// </summary>
    private async Task<IReadOnlyList<DepotSpec>> InstalledDepotsAsync(
        string root,
        IReadOnlyList<DepotSpec> fallback,
        CancellationToken cancellationToken)
    {
        InstallerState? state = await stores.LoadStateAsync(root, cancellationToken);
        if (state is null || state.Manifests.Count == 0)
        {
            return fallback;
        }

        return [.. state.Manifests
            .OrderBy(manifest => manifest.Key)
            .Select(manifest => new DepotSpec(manifest.Key, manifest.Value))];
    }

    private static async Task SaveStateAsync(
        string root,
        PreparedPayload payload,
        IEnumerable<DepotSpec> depots,
        CancellationToken cancellationToken)
    {
        InstallerState state = new()
        {
            ReleaseTag = payload.Release.Tag,
            ReleaseAsset = payload.Release.Asset.Name,
            ReleaseAssetDigest = payload.Release.Asset.Digest,
            InstalledDllSha256 = payload.DllSha256,
            InstalledAtUtc = DateTimeOffset.UtcNow,
            Manifests = depots.ToDictionary(depot => depot.DepotId, depot => depot.ManifestId),
        };
        await JsonStores.SaveStateAsync(root, state, cancellationToken);
    }

    private static void VerifyGameFiles(string root)
    {
        if (!File.Exists(Path.Combine(root, AppConstants.GameExecutableName)))
        {
            throw new InstallerException("DepotDownloader finished, but destiny2.exe is missing.");
        }

        if (!Directory.Exists(Path.Combine(root, "bin", "x64")))
        {
            throw new InstallerException("DepotDownloader finished, but bin\\x64 is missing.");
        }
    }

    private static void ValidateSteamUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new InstallerException("Enter the Steam account name that owns Destiny 2.");
        }

        if (username.Any(char.IsControl))
        {
            throw new InstallerException("The Steam account name contains an invalid character.");
        }
    }

    private static bool DigestsDiffer(string? installed, string? latest) =>
        !string.IsNullOrWhiteSpace(installed) &&
        !string.IsNullOrWhiteSpace(latest) &&
        !installed.Equals(latest, StringComparison.OrdinalIgnoreCase);

    private static Progress<int>? ScaleProgress(
        IProgress<OperationProgress>? progress,
        int start,
        int end)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<int>(value =>
            progress.Report(new OperationProgress("Downloading...", start + ((end - start) * value / 100))));
    }

    private static Progress<string>? MessageProgress(IProgress<OperationProgress>? progress)
    {
        if (progress is null)
        {
            return null;
        }

        return new Progress<string>(message => progress.Report(new OperationProgress(message)));
    }
}
