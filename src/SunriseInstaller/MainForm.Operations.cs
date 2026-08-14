using Sunrise.Installer.Services;

namespace Sunrise.Installer;

public sealed partial class MainForm
{
    private void BrowseButton_Click(object? sender, EventArgs eventArgs)
    {
        using FolderBrowserDialog dialog = new()
        {
            Description = "Choose the folder that the game will install to.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true,
            InitialDirectory = Directory.Exists(installPath.Text) ? installPath.Text : string.Empty,
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            installPath.Text = dialog.SelectedPath;
        }
    }

    private async void InstallButton_Click(object? sender, EventArgs eventArgs)
    {
        if (!ConfirmNonEmptyInstall())
        {
            return;
        }

        await RunOperationAsync(
            "Install",
            (progress, cancellationToken) => coordinator.InstallAsync(
                installPath.Text,
                steamUsername.Text,
                SelectedDepots(),
                progress,
                cancellationToken));
    }

    private async void RepairButton_Click(object? sender, EventArgs eventArgs)
    {
        DialogResult confirmation = MessageBox.Show(
            this,
            "Validate game and mod files. Also deletes " +
            "bin\\x64\\Sunrise, including your Sunrise config. Continue?",
            "Repair Sunrise",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);
        if (confirmation != DialogResult.Yes)
        {
            return;
        }

        await RunOperationAsync(
            "Repair",
            (progress, cancellationToken) => coordinator.RepairAsync(
                installPath.Text,
                steamUsername.Text,
                SelectedDepots(),
                progress,
                cancellationToken));
    }

    private async void UpdateButton_Click(object? sender, EventArgs eventArgs)
    {
        await RunOperationAsync(
            "Update",
            async (progress, cancellationToken) =>
            {
                bool updated = await coordinator.UpdateAsync(installPath.Text, progress, cancellationToken);
                if (!updated)
                {
                    MessageBox.Show(this, "Sunrise is already updated.", "Sunrise Installer");
                }
            });
    }

    private void CancelButton_Click(object? sender, EventArgs eventArgs) => operationCancellation?.Cancel();

    private async Task RunOperationAsync(
        string operationName,
        Func<IProgress<OperationProgress>, CancellationToken, Task> operation)
    {
        if (busy)
        {
            return;
        }

        busy = true;
        SetBusyState(true);
        operationCancellation = new CancellationTokenSource();
        Progress<OperationProgress> progress = new(UpdateProgress);

        try
        {
            await SavePreferencesAsync(operationCancellation.Token);
            activity.AppendText($"{operationName} started.{Environment.NewLine}");
            await operation(progress, operationCancellation.Token);
            activity.AppendText($"{operationName} complete.{Environment.NewLine}");
            MessageBox.Show(this, $"{operationName} complete.", "Sunrise Installer");
        }
        catch (OperationCanceledException)
        {
            status.Text = "Operation cancelled.";
            activity.AppendText($"{operationName} cancelled.{Environment.NewLine}");
        }
        catch (Exception exception)
        {
            ShowFailure(exception);
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            busy = false;
            SetBusyState(false);
            await RefreshLocalStatusAsync();
        }
    }

    private bool ConfirmNonEmptyInstall()
    {
        try
        {
            if (!Directory.Exists(installPath.Text) ||
                !Directory.EnumerateFileSystemEntries(installPath.Text).Any())
            {
                return true;
            }

            DialogResult result = MessageBox.Show(
                this,
                "The selected folder is not empty. Install may replace files in it. Continue?",
                "Sunrise Installer",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            return result == DialogResult.Yes;
        }
        catch (Exception exception)
        {
            ShowFailure(exception);
            return false;
        }
    }

    private void UpdateProgress(OperationProgress update)
    {
        status.Text = update.Message;
        activity.AppendText(update.Message + Environment.NewLine);
        activity.ScrollToCaret();
        if (update.Percent is int percent)
        {
            progressBar.Style = ProgressBarStyle.Continuous;
            progressBar.Value = Math.Clamp(percent, 0, 100);
        }
        else
        {
            progressBar.Style = ProgressBarStyle.Marquee;
        }
    }

    private void SetBusyState(bool isBusy)
    {
        installPath.Enabled = !isBusy;
        steamUsername.Enabled = !isBusy;
        gameVersion.Enabled = !isBusy;
        foreach (TextBox input in manifestInputs.Values)
        {
            input.Enabled = !isBusy;
        }

        browseButton.Enabled = !isBusy;
        installButton.Enabled = !isBusy;
        repairButton.Enabled = !isBusy;
        updateButton.Enabled = !isBusy;
        cancelButton.Enabled = isBusy;
        if (!isBusy)
        {
            progressBar.Style = ProgressBarStyle.Continuous;
        }
    }

    private void ShowFailure(Exception exception)
    {
        string message = ErrorMessages.For(exception);
        status.Text = message;
        activity.AppendText($"Error: {message}{Environment.NewLine}");
        log.Error(
            "operation_failed",
            message,
            ("err", exception.GetType().Name),
            ("code", exception.HResult));
        MessageBox.Show(this, message, "Sunrise Installer", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private void OnLogMessage(string message)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(() => OnLogMessage(message));
            return;
        }

        activity.AppendText(message + Environment.NewLine);
        activity.ScrollToCaret();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs eventArgs)
    {
        if (!busy)
        {
            return;
        }

        eventArgs.Cancel = true;
        MessageBox.Show(this, "Cancel the active operation before closing the installer.", "Sunrise Installer");
    }
}
