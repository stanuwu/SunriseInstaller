using System.Globalization;

namespace Sunrise.Installer;

public sealed partial class MainForm
{
    private const string CustomVersionName = "Custom...";

    private bool IsCustomVersion => gameVersion.SelectedIndex >= AppConstants.KnownVersions.Length;

    private GameVersion SelectedKnownVersion =>
        AppConstants.KnownVersions[Math.Clamp(gameVersion.SelectedIndex, 0, AppConstants.KnownVersions.Length - 1)];

    private TableLayoutPanel BuildVersionPanel()
    {
        TableLayoutPanel panel = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 0, 14),
        };
        panel.Controls.Add(FieldLabel("Game version"), 0, 0);

        gameVersion.Dock = DockStyle.Top;
        gameVersion.DropDownStyle = ComboBoxStyle.DropDownList;
        gameVersion.Margin = new Padding(0, 5, 0, 6);
        foreach (GameVersion version in AppConstants.KnownVersions)
        {
            gameVersion.Items.Add(version.Name);
        }

        gameVersion.Items.Add(CustomVersionName);
        gameVersion.SelectedIndex = 0;
        gameVersion.SelectedIndexChanged += async (_, _) =>
        {
            ShowSelectedVersion();
            await RefreshLocalStatusAsync();
        };
        panel.Controls.Add(gameVersion, 0, 1);

        manifestSummary.AutoSize = true;
        manifestSummary.Font = new Font("Consolas", 8.5F);
        manifestSummary.ForeColor = Color.FromArgb(100, 116, 139);
        manifestSummary.Margin = new Padding(2, 0, 0, 0);
        panel.Controls.Add(manifestSummary, 0, 2);
        panel.Controls.Add(BuildCustomManifestPanel(), 0, 3);

        Label help = new()
        {
            Text = "Sunrise is built for one game build. Another build downloads fine but will likely break the mod.",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(2, 6, 0, 0),
        };
        panel.Controls.Add(help, 0, 4);
        ShowSelectedVersion();
        return panel;
    }

    private TableLayoutPanel BuildCustomManifestPanel()
    {
        customManifests.Dock = DockStyle.Top;
        customManifests.AutoSize = true;
        customManifests.ColumnCount = 2;
        customManifests.Margin = new Padding(0, 2, 0, 0);
        customManifests.Visible = false;
        customManifests.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        customManifests.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;
        foreach (uint depotId in AppConstants.DepotIds)
        {
            Label label = new()
            {
                Text = $"Depot {depotId}",
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 7, 10, 0),
            };
            TextBox input = new()
            {
                Dock = DockStyle.Fill,
                PlaceholderText = "manifest id",
                Margin = new Padding(0, 3, 0, 3),
            };
            manifestInputs[depotId] = input;
            customManifests.Controls.Add(label, 0, row);
            customManifests.Controls.Add(input, 1, row);
            row++;
        }

        Label source = new()
        {
            Text = "Manifest ids come from the depot history on steamdb.info.",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 4, 0, 0),
        };
        customManifests.Controls.Add(source, 0, row);
        customManifests.SetColumnSpan(source, 2);
        return customManifests;
    }

    private void ShowSelectedVersion()
    {
        bool custom = IsCustomVersion;
        customManifests.Visible = custom;
        manifestSummary.Visible = !custom;
        manifestSummary.Text = custom
            ? string.Empty
            : string.Join(
                Environment.NewLine,
                SelectedKnownVersion.Depots.Select(depot => $"{depot.DepotId}  {depot.ManifestId}"));
    }

    /// <summary>The depots to download, or an <see cref="InstallerException"/> when a custom id is unusable.</summary>
    private IReadOnlyList<DepotSpec> SelectedDepots()
    {
        if (!TryGetSelectedDepots(out IReadOnlyList<DepotSpec> depots, out string error))
        {
            throw new InstallerException(error);
        }

        return depots;
    }

    private bool TryGetSelectedDepots(out IReadOnlyList<DepotSpec> depots, out string error)
    {
        error = string.Empty;
        if (!IsCustomVersion)
        {
            depots = SelectedKnownVersion.Depots;
            return true;
        }

        List<DepotSpec> custom = [];
        foreach (uint depotId in AppConstants.DepotIds)
        {
            if (!TryReadManifest(manifestInputs[depotId].Text, out ulong manifestId))
            {
                depots = [];
                error = $"Enter the manifest id for depot {depotId}. A manifest id is a plain number.";
                return false;
            }

            custom.Add(new DepotSpec(depotId, manifestId));
        }

        depots = custom;
        return true;
    }

    private void ApplyVersionPreference(UserPreferences preferences)
    {
        foreach ((uint depotId, ulong manifestId) in preferences.CustomManifests)
        {
            if (manifestInputs.TryGetValue(depotId, out TextBox? input))
            {
                input.Text = manifestId.ToString(CultureInfo.InvariantCulture);
            }
        }

        int index = Array.FindIndex(
            AppConstants.KnownVersions,
            version => version.Name.Equals(preferences.GameVersion, StringComparison.Ordinal));
        if (index >= 0)
        {
            gameVersion.SelectedIndex = index;
        }
        else if (preferences.GameVersion.Equals(CustomVersionName, StringComparison.Ordinal))
        {
            gameVersion.SelectedIndex = AppConstants.KnownVersions.Length;
        }

        ShowSelectedVersion();
    }

    private void CollectVersionPreference(UserPreferences preferences)
    {
        preferences.GameVersion = IsCustomVersion ? CustomVersionName : SelectedKnownVersion.Name;
        foreach ((uint depotId, TextBox input) in manifestInputs)
        {
            if (TryReadManifest(input.Text, out ulong manifestId))
            {
                preferences.CustomManifests[depotId] = manifestId;
            }
        }
    }

    /// <summary>True when the installed manifests are the ones the selected version would download.</summary>
    private bool MatchesSelectedVersion(Dictionary<uint, ulong> manifests)
    {
        if (!TryGetSelectedDepots(out IReadOnlyList<DepotSpec> depots, out _))
        {
            return true;
        }

        return depots.Count == manifests.Count &&
            depots.All(depot =>
                manifests.TryGetValue(depot.DepotId, out ulong manifestId) && manifestId == depot.ManifestId);
    }

    private static bool TryReadManifest(string text, out ulong manifestId) =>
        ulong.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out manifestId) &&
        manifestId != 0;
}
