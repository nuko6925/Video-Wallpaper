using LibVLCSharp.Shared;

namespace VideoWallpaper;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        using var mutex = new Mutex(
            initiallyOwned: true,
            name: @"Local\VideoWallpaper.SingleInstance",
            createdNew: out var createdNew);

        if (!createdNew)
        {
            MessageBox.Show(
                "Video Wallpaperは既に起動しています。",
                "Video Wallpaper",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            return;
        }

        try
        {
            Core.Initialize();

            using var form = new WallpaperForm();
            Application.Run(form);
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                exception.ToString(),
                "起動エラー",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}