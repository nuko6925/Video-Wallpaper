using Microsoft.Win32;

namespace VideoWallpaper;

internal sealed class WallpaperForm : Form
{
    private readonly AppSettings _settings;
    private readonly DesktopWallpaperHost _desktopHost;
    private readonly WallpaperPlayer _player;

    private readonly NotifyIcon _trayIcon;
    private readonly ToolStripMenuItem _playPauseMenuItem;
    private readonly ToolStripMenuItem _muteMenuItem;
    private readonly ToolStripMenuItem _currentVideoMenuItem;

    private readonly System.Windows.Forms.Timer _desktopMonitorTimer;

    private bool _allowClose;
    private bool _initialized;

    public WallpaperForm()
    {
        _settings = AppSettings.Load();
        _desktopHost = new DesktopWallpaperHost();
        _player = new WallpaperPlayer();

        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;

        Controls.Add(_player.View);
        Resize += (_, _) => ResizeVideoView();

        _playPauseMenuItem = new ToolStripMenuItem(
            "再生 / 一時停止",
            null,
            (_, _) =>
            {
                _player.TogglePause();
                UpdateTrayState();
            });

        _muteMenuItem = new ToolStripMenuItem(
            "ミュート",
            null,
            (_, _) =>
            {
                _player.Muted = !_player.Muted;
                SavePlayerSettings();
                UpdateTrayState();
            });

        _currentVideoMenuItem = new ToolStripMenuItem(
            "動画未選択")
        {
            Enabled = false
        };

        ContextMenuStrip trayMenu = CreateTrayMenu();

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Video Wallpaper",
            ContextMenuStrip = trayMenu,
            Visible = false
        };

        _trayIcon.DoubleClick += (_, _) =>
            SelectVideo();

        _desktopMonitorTimer =
            new System.Windows.Forms.Timer
            {
                Interval = 2000
            };

        _desktopMonitorTimer.Tick += (_, _) =>
            CheckDesktopHost();

        Shown += OnShown;
        FormClosing += OnFormClosing;

        SystemEvents.DisplaySettingsChanged +=
            OnDisplaySettingsChanged;
    }
    
    private void ResizeVideoView()
    {
        _player.View.SetBounds(
            -1,
            -1,
            ClientSize.Width + 2,
            ClientSize.Height + 2);
    }

    protected override bool ShowWithoutActivation =>
        true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int wsExToolWindow = 0x00000080;
            const int wsExNoActivate = 0x08000000;

            CreateParams parameters = base.CreateParams;

            parameters.ExStyle |= wsExToolWindow;
            parameters.ExStyle |= wsExNoActivate;

            return parameters;
        }
    }

    private ContextMenuStrip CreateTrayMenu()
    {
        var menu = new ContextMenuStrip();

        menu.Items.Add(_currentVideoMenuItem);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(
            "動画を選択...",
            null,
            (_, _) => SelectVideo());

        menu.Items.Add(_playPauseMenuItem);
        menu.Items.Add(
            "停止",
            null,
            (_, _) =>
            {
                _player.Stop();
                UpdateTrayState();
            });

        var volumeMenu =
            new ToolStripMenuItem("音量");

        foreach (int volume in
                 new[] { 100, 75, 50, 25, 10, 0 })
        {
            int selectedVolume = volume;

            volumeMenu.DropDownItems.Add(
                $"{selectedVolume}%",
                null,
                (_, _) =>
                {
                    _player.Volume = selectedVolume;
                    _player.Muted = false;

                    SavePlayerSettings();
                    UpdateTrayState();
                });
        }

        volumeMenu.DropDownItems.Add(
            new ToolStripSeparator());

        volumeMenu.DropDownItems.Add(_muteMenuItem);

        menu.Items.Add(volumeMenu);
        menu.Items.Add(new ToolStripSeparator());

        menu.Items.Add(
            "終了",
            null,
            (_, _) =>
            {
                _allowClose = true;
                Close();
            });

        menu.Opening += (_, _) =>
            UpdateTrayState();

        return menu;
    }

    private void OnShown(
        object? sender,
        EventArgs e)
    {
        if (_initialized)
            return;

        _initialized = true;
        
        _trayIcon.Visible = true;
        _trayIcon.ShowBalloonTip(
            3000,
            "Video Wallpaper",
            "アプリを起動しました。",
            ToolTipIcon.Info);

        Screen primaryScreen =
            Screen.PrimaryScreen
            ?? throw new InvalidOperationException(
                "メインモニターを取得できませんでした。");

        Bounds = primaryScreen.Bounds;
        _player.SetFillSize(
            primaryScreen.Bounds.Width,
            primaryScreen.Bounds.Height);

        _desktopHost.Attach(
            this,
            primaryScreen);

        if (File.Exists(_settings.VideoPath))
        {
            try
            {
                _player.PlayFile(
                    _settings.VideoPath);

                _player.Volume = _settings.Volume;
                _player.Muted = _settings.Muted;
            }
            catch (Exception exception)
            {
                ShowTrayError(
                    "保存されていた動画を再生できませんでした。",
                    exception);
            }
        }

        _desktopMonitorTimer.Start();

        UpdateTrayState();
    }

    private void SelectVideo()
    {
        using var dialog = new OpenFileDialog();
        dialog.Title = "背景動画を選択";
        dialog.Filter = "動画ファイル|" +
                        "*.mp4;*.mkv;*.webm;*.avi;*.mov;*.wmv;*.m4v;*.ts|" +
                        "すべてのファイル|*.*";
        dialog.CheckFileExists = true;
        dialog.Multiselect = false;
        dialog.RestoreDirectory = true;

        if (!string.IsNullOrWhiteSpace(
                _settings.VideoPath))
        {
            string? directory =
                Path.GetDirectoryName(
                    _settings.VideoPath);

            if (Directory.Exists(directory))
                dialog.InitialDirectory = directory;
        }

        if (dialog.ShowDialog() !=
            DialogResult.OK)
        {
            return;
        }

        try
        {
            _player.PlayFile(dialog.FileName);

            _settings.VideoPath =
                dialog.FileName;

            SavePlayerSettings();
            UpdateTrayState();
        }
        catch (Exception exception)
        {
            ShowTrayError(
                "動画を再生できませんでした。",
                exception);
        }
    }

    private void CheckDesktopHost()
    {
        if (!_initialized ||
            IsDisposed ||
            Disposing)
        {
            return;
        }

        var primaryScreen =
            Screen.PrimaryScreen;

        if (primaryScreen is null)
            return;

        try
        {
            _desktopHost.ReattachIfRequired(
                this,
                primaryScreen);
        }
        catch
        {
            // wait for next tick.
        }
    }

    private void OnDisplaySettingsChanged(
        object? sender,
        EventArgs e)
    {
        if (IsDisposed || Disposing)
            return;

        BeginInvoke(() =>
        {
            Screen? primaryScreen =
                Screen.PrimaryScreen;

            
            if (primaryScreen is null)
                return;

            _player.SetFillSize(
                primaryScreen.Bounds.Width,
                primaryScreen.Bounds.Height);
            
            try
            {
                _desktopHost.Attach(
                    this,
                    primaryScreen);
                ResizeVideoView();
            }
            catch (Exception exception)
            {
                ShowTrayError(
                    "モニター構成変更後の再配置に失敗しました。",
                    exception);
            }
        });
    }

    private void SavePlayerSettings()
    {
        _settings.Volume = _player.Volume;
        _settings.Muted = _player.Muted;

        try
        {
            _settings.Save();
        }
        catch (Exception exception)
        {
            ShowTrayError(
                "設定を保存できませんでした。",
                exception);
        }
    }

    private void UpdateTrayState()
    {
        string videoName =
            string.IsNullOrWhiteSpace(
                _settings.VideoPath)
                ? "動画未選択"
                : Path.GetFileName(
                    _settings.VideoPath);

        _currentVideoMenuItem.Text =
            videoName;

        _playPauseMenuItem.Enabled =
            !string.IsNullOrWhiteSpace(
                _settings.VideoPath) &&
            File.Exists(_settings.VideoPath);

        _muteMenuItem.Checked =
            _player.Muted;

        string state = _player.IsPlaying
            ? "再生中"
            : "停止中";

        string mute = _player.Muted
            ? "・ミュート"
            : string.Empty;

        string tooltip =
            $"Video Wallpaper - {state} " +
            $"({_player.Volume}%{mute})";

        _trayIcon.Text =
            tooltip.Length <= 63
                ? tooltip
                : tooltip[..63];
    }

    private void ShowTrayError(
        string message,
        Exception exception)
    {
        string text =
            $"{message}{Environment.NewLine}" +
            exception.Message;

        _trayIcon.ShowBalloonTip(
            5000,
            "Video Wallpaper",
            text,
            ToolTipIcon.Error);
    }

    private void OnFormClosing(
        object? sender,
        FormClosingEventArgs e)
    {
        if (!_allowClose)
        {
            // cancel alt f4.
            e.Cancel = true;
            return;
        }

        _desktopMonitorTimer.Stop();

        SystemEvents.DisplaySettingsChanged -=
            OnDisplaySettingsChanged;

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        _desktopMonitorTimer.Dispose();
        _player.Dispose();
    }
}