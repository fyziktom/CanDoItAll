namespace CanDoItAll.Mcp.DotNetWatch.Tray;

internal sealed class BackendTrayApplicationContext : ApplicationContext
{
    private readonly TrayOptions _options;
    private readonly BackendTrayController _controller;
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ToolStripMenuItem _detailItem;
    private readonly ToolStripMenuItem _openManagerItem;
    private readonly ToolStripMenuItem _startRecoverItem;
    private readonly ToolStripMenuItem _restartItem;
    private readonly ToolStripMenuItem _openLogsItem;
    private readonly ToolStripMenuItem _refreshItem;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private BackendTraySnapshot _snapshot;
    private string? _lastAnnouncedKey;
    private bool _disposed;

    public BackendTrayApplicationContext(TrayOptions options)
    {
        _options = options;
        _controller = new BackendTrayController(options);
        _snapshot = BackendTraySnapshot.Initial(options);

        _statusItem = new ToolStripMenuItem("Status: starting")
        {
            Enabled = false
        };
        _detailItem = new ToolStripMenuItem("Workspace: pending")
        {
            Enabled = false
        };
        _openManagerItem = new ToolStripMenuItem("Open Backend Manager", null, (_, _) => OpenManagerPage());
        _startRecoverItem = new ToolStripMenuItem("Start Or Recover Backend", null, async (_, _) => await StartOrRecoverBackendAsync(forceRestart: false));
        _restartItem = new ToolStripMenuItem("Restart Matching Backend(s)", null, async (_, _) => await StartOrRecoverBackendAsync(forceRestart: true));
        _openLogsItem = new ToolStripMenuItem("Open Logs Folder", null, (_, _) => OpenLogsFolder());
        _refreshItem = new ToolStripMenuItem("Refresh Now", null, async (_, _) => await RefreshAsync(allowNotification: true));
        var exitItem = new ToolStripMenuItem("Exit", null, (_, _) => ExitThread());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(
        [
            _statusItem,
            _detailItem,
            new ToolStripSeparator(),
            _openManagerItem,
            _startRecoverItem,
            _restartItem,
            _openLogsItem,
            _refreshItem,
            new ToolStripSeparator(),
            exitItem
        ]);

        _notifyIcon = new NotifyIcon
        {
            ContextMenuStrip = menu,
            Icon = SystemIcons.Application,
            Text = BackendTraySnapshot.TrimNotifyText("CanDoItAll MCP: starting tray"),
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => OpenManagerPage();

        _refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = (int)Math.Max(1000, options.PollInterval.TotalMilliseconds)
        };
        _refreshTimer.Tick += async (_, _) => await RefreshAsync(allowNotification: true);
        _refreshTimer.Start();

        _controller.WriteLog($"tray start | repo={options.RepoRoot} | settings={options.SettingsPath}");
        _ = RefreshAsync(allowNotification: false);
    }

    protected override void ExitThreadCore()
    {
        _refreshTimer.Stop();
        base.ExitThreadCore();
    }

    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            base.Dispose(disposing);
            return;
        }

        if (disposing)
        {
            _refreshTimer.Dispose();
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _controller.Dispose();
            _refreshGate.Dispose();
        }

        _disposed = true;
        base.Dispose(disposing);
    }

    private async Task RefreshAsync(bool allowNotification)
    {
        if (!await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var next = await _controller.GetSnapshotAsync();
            _snapshot = next;
            UpdateUi(next);

            if (!allowNotification)
            {
                _lastAnnouncedKey = next.NotificationKey;
                return;
            }

            AnnounceIfNeeded(next);
        }
        catch (Exception ex)
        {
            _controller.WriteLog($"refresh failed | error={ex.Message}");
            var error = BackendTraySnapshot.Error(ex.Message);
            _snapshot = error;
            UpdateUi(error);
            AnnounceIfNeeded(error);
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void UpdateUi(BackendTraySnapshot snapshot)
    {
        _statusItem.Text = $"Status: {snapshot.MenuText}";
        _detailItem.Text = $"Workspace: {Path.GetFileName(_options.RepoRoot)}";
        _openManagerItem.Enabled = snapshot.PrimaryBackend?.IsReachable == true &&
            !string.IsNullOrWhiteSpace(snapshot.PrimaryBackend.Record.Registration.ManagerUrl);
        _startRecoverItem.Enabled = snapshot.CanStartOrRecover;
        _restartItem.Enabled = snapshot.CanRestart;
        _notifyIcon.Text = BackendTraySnapshot.TrimNotifyText(snapshot.TooltipText);
        _notifyIcon.Icon = snapshot.StatusKind switch
        {
            TrayStatusKind.Healthy => SystemIcons.Information,
            TrayStatusKind.Duplicate => SystemIcons.Warning,
            TrayStatusKind.Unreachable => SystemIcons.Error,
            TrayStatusKind.Error => SystemIcons.Error,
            _ => SystemIcons.Application
        };
    }

    private void AnnounceIfNeeded(BackendTraySnapshot snapshot)
    {
        if (string.Equals(_lastAnnouncedKey, snapshot.NotificationKey, StringComparison.Ordinal))
        {
            return;
        }

        _lastAnnouncedKey = snapshot.NotificationKey;
        if (string.IsNullOrWhiteSpace(snapshot.NotificationText))
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "CanDoItAll DotNetWatch";
        _notifyIcon.BalloonTipIcon = snapshot.NotificationIcon;
        _notifyIcon.BalloonTipText = snapshot.NotificationText;
        _notifyIcon.ShowBalloonTip(5000);
        _controller.WriteLog($"notify | state={snapshot.StatusKind} | text={snapshot.NotificationText}");
    }

    private async Task StartOrRecoverBackendAsync(bool forceRestart)
    {
        try
        {
            var requiresStop = forceRestart || _snapshot.MatchingBackends.Any(static candidate => candidate.IsLive);
            if (requiresStop)
            {
                var message = forceRestart
                    ? "Restart the matching backend processes for this workspace? This can interrupt active MCP calls."
                    : "Recover the backend by restarting the matching backend processes for this workspace?";
                var result = MessageBox.Show(
                    message,
                    forceRestart ? "Restart DotNetWatch Backend" : "Recover DotNetWatch Backend",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);
                if (result != DialogResult.Yes)
                {
                    return;
                }
            }

            var next = await _controller.RecoverAsync(_snapshot, forceRestart);
            _snapshot = next;
            UpdateUi(next);
            AnnounceIfNeeded(next);
            _controller.OpenManagerPage(next);
        }
        catch (Exception ex)
        {
            _controller.WriteLog($"backend recover failed | error={ex.Message}");
            MessageBox.Show(
                ex.Message,
                "CanDoItAll DotNetWatch Tray",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OpenManagerPage()
    {
        _controller.OpenManagerPage(_snapshot);
    }

    private void OpenLogsFolder()
    {
        _controller.OpenLogsFolder();
    }
}
