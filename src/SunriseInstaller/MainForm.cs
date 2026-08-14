using Sunrise.Installer.Services;

namespace Sunrise.Installer;

public sealed partial class MainForm : Form
{
    private readonly AppOptions options;
    private readonly InstallerLog log = new();
    private readonly InstallCoordinator coordinator;
    private readonly TextBox installPath = new();
    private readonly TextBox steamUsername = new();
    private readonly ComboBox gameVersion = new();
    private readonly Label manifestSummary = new();
    private readonly TableLayoutPanel customManifests = new();
    private readonly Dictionary<uint, TextBox> manifestInputs = [];
    private readonly Label status = new();
    private readonly ProgressBar progressBar = new();
    private readonly RichTextBox activity = new();
    private readonly Button browseButton = new();
    private readonly Button installButton = new();
    private readonly Button repairButton = new();
    private readonly Button updateButton = new();
    private readonly Button cancelButton = new();
    private CancellationTokenSource? operationCancellation;
    private bool busy;

    public MainForm(AppOptions options)
    {
        this.options = options;
        coordinator = new InstallCoordinator(log, options);
        Text = options.IsTestMode ? "Sunrise Installer - Test Mode" : "Sunrise Installer";
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? Icon;
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 590);
        Size = new Size(820, 650);
        Font = new Font("Segoe UI", 9F);
        BackColor = Color.FromArgb(245, 247, 250);
        BuildLayout();
        log.MessageWritten += OnLogMessage;
        Shown += async (_, _) => await LoadPreferencesAsync();
        FormClosing += OnFormClosing;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            operationCancellation?.Cancel();
            operationCancellation?.Dispose();
            coordinator.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task LoadPreferencesAsync()
    {
        try
        {
            UserPreferences preferences = await InstallCoordinator.LoadPreferencesAsync(CancellationToken.None);
            installPath.Text = preferences.InstallDirectory;
            steamUsername.Text = preferences.SteamUsername;
            ApplyVersionPreference(preferences);
            await RefreshLocalStatusAsync();
        }
        catch (Exception exception)
        {
            ShowFailure(exception);
        }
    }

    private async Task RefreshLocalStatusAsync()
    {
        if (busy || string.IsNullOrWhiteSpace(installPath.Text))
        {
            return;
        }

        try
        {
            string installDirectory = Path.GetFullPath(installPath.Text.Trim());
            InstallerState? state = await coordinator.LoadStateAsync(installDirectory, CancellationToken.None);
            status.Text = DescribeInstall(installDirectory, state);
        }
        catch
        {
            status.Text = "The install folder path is not valid.";
        }
    }

    private string DescribeInstall(string installDirectory, InstallerState? state)
    {
        string summary = state is null
            ? "No Sunrise install was found in this folder."
            : $"Installed Sunrise release: {state.ReleaseTag}";
        if (GameBuild.Read(installDirectory) is string build)
        {
            summary += $"   |   Game: {build}";
        }

        if (state is not null && state.Manifests.Count > 0 && !MatchesSelectedVersion(state.Manifests))
        {
            summary += "   |   Installed version differs from the selected one. Install to change it.";
        }

        return summary;
    }

    private Task SavePreferencesAsync(CancellationToken cancellationToken)
    {
        UserPreferences preferences = new()
        {
            InstallDirectory = installPath.Text.Trim(),
            SteamUsername = steamUsername.Text.Trim(),
        };
        CollectVersionPreference(preferences);
        return InstallCoordinator.SavePreferencesAsync(preferences, cancellationToken);
    }
}
