using LibVLCSharp.Shared;
using LibVLCSharp.WinForms;

namespace VideoWallpaper;

internal sealed class WallpaperPlayer : IDisposable
{
    private int _targetWidth;
    private int _targetHeight;
    
    private readonly LibVLC _libVlc;
    private readonly MediaPlayer _mediaPlayer;

    private Media? _currentMedia;
    private bool _disposed;
    
    private string? _currentPath;
    private bool _manualStop;
    private bool _loopRestartPending;

    public VideoView View { get; }

    public bool IsPlaying =>
        _mediaPlayer.IsPlaying;

    public int Volume
    {
        get => _mediaPlayer.Volume;
        set => _mediaPlayer.Volume =
            Math.Clamp(value, 0, 100);
    }

    public bool Muted
    {
        get => _mediaPlayer.Mute;
        set => _mediaPlayer.Mute = value;
    }

    public WallpaperPlayer()
    {
        _libVlc = new LibVLC(
            "--no-video-title-show",
            "--no-sub-autodetect-file",
            "--avcodec-hw=none",
            "--vout=wingdi",
            "--quiet");

        _mediaPlayer = new MediaPlayer(_libVlc);
        
        _mediaPlayer.Playing += (_, _) =>
        {
            ApplyFillSize();
        };

        View = new VideoView
        {
            Dock = DockStyle.None,
            Location = new Point(0, -1),
            BackColor = Color.Black,
            MediaPlayer = _mediaPlayer
        };
    }

    public void PlayFile(string path)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException(
                "動画ファイルが指定されていません。",
                nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "動画ファイルが見つかりません。",
                path);
        }

        _manualStop = false;
        _currentPath = path;

        StopInternal(clearPath: false);
        var mediaOptions = new[] { "input-repeat=65535", "repeat" };

        using var media = new Media(_libVlc, path);
        media.AddOption(string.Join(" ", mediaOptions));
            
        _currentMedia = media;
            
        if (!_mediaPlayer.Play(media))
        {
            _currentMedia.Dispose();
            _currentMedia = null;

            throw new InvalidOperationException(
                "LibVLCが動画の再生開始に失敗しました。");
        }
    }

    public void Pause()
    {
        if (_mediaPlayer.IsPlaying)
            _mediaPlayer.Pause();
    }

    public void Resume()
    {
        if (_currentMedia is null)
            return;

        if (!_mediaPlayer.IsPlaying)
            _mediaPlayer.Play();
    }

    public void TogglePause()
    {
        if (_mediaPlayer.IsPlaying)
            Pause();
        else
            Resume();
    }

    public void Stop()
    {
        _manualStop = true;
        _loopRestartPending = false;

        StopInternal(clearPath: true);
    }

    private void StopInternal(bool clearPath)
    {
        _mediaPlayer.Stop();

        _currentMedia?.Dispose();
        _currentMedia = null;

        if (clearPath)
            _currentPath = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        Stop();

        View.MediaPlayer = null;
        View.Dispose();

        _mediaPlayer.Dispose();
        _libVlc.Dispose();
    }
    
    public void SetFillSize(int width, int height)
    {
        ObjectDisposedException.ThrowIf(
            _disposed,
            this);

        if (width <= 0 || height <= 0)
            return;

        _targetWidth = width;
        _targetHeight = height;

        ApplyFillSize();
    }

    private void ApplyFillSize()
    {
        if (_targetWidth <= 0 ||
            _targetHeight <= 0)
        {
            return;
        }

        int divisor = GreatestCommonDivisor(
            _targetWidth,
            _targetHeight);

        int aspectWidth =
            _targetWidth / divisor;

        int aspectHeight =
            _targetHeight / divisor;

        _mediaPlayer.CropGeometry =
            $"{aspectWidth}:{aspectHeight}";
    }

    private static int GreatestCommonDivisor(
        int left,
        int right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);

        while (right != 0)
        {
            int remainder = left % right;

            left = right;
            right = remainder;
        }

        return left == 0 ? 1 : left;
    }
}