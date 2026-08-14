namespace Sunrise.Installer;

public sealed partial class MainForm
{
    private void BuildLayout()
    {
        TableLayoutPanel root = new()
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(28, 22, 28, 22),
            ColumnCount = 1,
            RowCount = 9,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        Label title = new()
        {
            Text = "Sunrise",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 24F),
            ForeColor = Color.FromArgb(30, 41, 59),
            Margin = new Padding(0, 0, 0, 4),
        };
        Label subtitle = new()
        {
            Text = options.IsTestMode
                ? $"Test mode: using local DLL {Path.GetFileName(options.TestPayloadPath)}."
                : "Install Game Depots and Sunrise.",
            AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105),
            Margin = new Padding(2, 0, 0, 20),
        };
        root.Controls.Add(title, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(BuildFolderRow(), 0, 2);
        root.Controls.Add(BuildVersionPanel(), 0, 3);
        root.Controls.Add(BuildSteamPanel(), 0, 4);
        root.Controls.Add(BuildActionRow(), 0, 5);
        root.Controls.Add(BuildStatusPanel(), 0, 6);

        activity.Dock = DockStyle.Fill;
        activity.ReadOnly = true;
        activity.BackColor = Color.White;
        activity.BorderStyle = BorderStyle.FixedSingle;
        activity.Font = new Font("Consolas", 9F);
        activity.DetectUrls = true;
        activity.Margin = new Padding(0, 12, 0, 8);
        root.Controls.Add(activity, 0, 7);

        Label footer = new()
        {
            Text = "Repair deletes bin\\x64\\Sunrise, including its config. If you changed your config back it up now.",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0, 4, 0, 0),
        };
        root.Controls.Add(footer, 0, 8);
        Controls.Add(root);
    }

    private TableLayoutPanel BuildFolderRow()
    {
        TableLayoutPanel panel = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 14),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        Label label = FieldLabel("Install folder");
        panel.Controls.Add(label, 0, 0);
        panel.SetColumnSpan(label, 2);
        installPath.Dock = DockStyle.Fill;
        installPath.PlaceholderText = @"D:\Games\Sunrise";
        installPath.Margin = new Padding(0, 5, 8, 0);
        installPath.TextChanged += async (_, _) => await RefreshLocalStatusAsync();
        panel.Controls.Add(installPath, 0, 1);

        browseButton.Text = "Browse...";
        browseButton.AutoSize = true;
        browseButton.Margin = new Padding(0, 4, 0, 0);
        browseButton.Click += BrowseButton_Click;
        panel.Controls.Add(browseButton, 1, 1);
        return panel;
    }

    private TableLayoutPanel BuildSteamPanel()
    {
        TableLayoutPanel panel = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Color.White,
            Padding = new Padding(14),
            Margin = new Padding(0, 0, 0, 14),
        };
        panel.Controls.Add(FieldLabel("Steam account name"), 0, 0);
        steamUsername.Dock = DockStyle.Top;
        steamUsername.Margin = new Padding(0, 5, 0, 8);
        panel.Controls.Add(steamUsername, 0, 1);

        Label help = new()
        {
            Text = "A console opens for password and Steam Guard prompts.",
            AutoSize = true,
            ForeColor = Color.FromArgb(100, 116, 139),
            Margin = new Padding(0),
        };
        panel.Controls.Add(help, 0, 2);
        return panel;
    }

    private FlowLayoutPanel BuildActionRow()
    {
        FlowLayoutPanel panel = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0, 0, 0, 14),
        };
        ConfigureActionButton(installButton, "Install", InstallButton_Click, primary: true);
        ConfigureActionButton(repairButton, "Repair", RepairButton_Click);
        ConfigureActionButton(updateButton, "Check / Update", UpdateButton_Click);
        ConfigureActionButton(cancelButton, "Cancel", CancelButton_Click);
        cancelButton.Enabled = false;
        panel.Controls.AddRange([installButton, repairButton, updateButton, cancelButton]);
        return panel;
    }

    private TableLayoutPanel BuildStatusPanel()
    {
        TableLayoutPanel panel = new()
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0),
        };
        status.Text = "Select an install folder.";
        status.AutoSize = true;
        status.ForeColor = Color.FromArgb(51, 65, 85);
        status.Margin = new Padding(0, 0, 0, 6);
        progressBar.Dock = DockStyle.Top;
        progressBar.Height = 8;
        progressBar.Style = ProgressBarStyle.Continuous;
        panel.Controls.Add(status, 0, 0);
        panel.Controls.Add(progressBar, 0, 1);
        return panel;
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI Semibold", 9F),
        ForeColor = Color.FromArgb(51, 65, 85),
    };

    private static void ConfigureActionButton(
        Button button,
        string text,
        EventHandler handler,
        bool primary = false)
    {
        button.Text = text;
        button.AutoSize = true;
        button.MinimumSize = new Size(110, 36);
        button.Margin = new Padding(0, 0, 10, 0);
        button.Click += handler;
        if (primary)
        {
            button.BackColor = Color.FromArgb(37, 99, 235);
            button.ForeColor = Color.White;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
        }
    }
}
