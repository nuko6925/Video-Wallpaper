using System.Text.Json;

namespace VideoWallpaper;

internal sealed class AppSettings
{
    public string VideoPath { get; set; } = string.Empty;

    public int Volume { get; set; } = 50;

    public bool Muted { get; set; }

    private static string SettingsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData),
            "VideoWallpaper");

    private static string SettingsPath =>
        Path.Combine(SettingsDirectory, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new AppSettings();

            string json = File.ReadAllText(SettingsPath);

            var settings =
                JsonSerializer.Deserialize<AppSettings>(json)
                ?? new AppSettings();

            settings.Volume = Math.Clamp(settings.Volume, 0, 100);

            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);

        string temporaryPath = SettingsPath + ".tmp";

        string json = JsonSerializer.Serialize(
            this,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(temporaryPath, json);

        File.Move(
            temporaryPath,
            SettingsPath,
            overwrite: true);
    }
}