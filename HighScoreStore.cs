using System.Text.Json;
using Windows.Storage;

namespace WinSnake;

/// <summary>
/// Persists the best score. Packaged runs use the package's local settings;
/// portable runs (no package identity) fall back to a file under LocalAppData.
/// </summary>
internal static class HighScoreStore
{
    private const string SettingsKey = "HighScore";
    private const string AppFolderName = "WinSnake";
    private const string FileName = "highscore.json";

    public static int Load()
    {
        int score = 0;

        try
        {
            if (ApplicationData.Current.LocalSettings.Values.TryGetValue(SettingsKey, out object? value)
                && value is int packagedScore)
            {
                score = packagedScore;
            }
        }
        catch (Exception)
        {
            // Package identity is unavailable in the portable build.
        }

        try
        {
            string path = GetFallbackPath();
            if (File.Exists(path))
            {
                HighScoreData? data = JsonSerializer.Deserialize<HighScoreData>(File.ReadAllText(path));
                score = Math.Max(score, data?.HighScore ?? 0);
            }
        }
        catch (Exception)
        {
            // A missing or unreadable file must never block the game from starting.
        }

        return Math.Max(0, score);
    }

    public static void Save(int highScore)
    {
        if (highScore <= 0)
        {
            return;
        }

        try
        {
            ApplicationData.Current.LocalSettings.Values[SettingsKey] = highScore;
        }
        catch (Exception)
        {
            // Package identity is unavailable in the portable build.
        }

        try
        {
            string path = GetFallbackPath();
            string? directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, JsonSerializer.Serialize(new HighScoreData { HighScore = highScore }));
        }
        catch (Exception)
        {
            // Best effort only; the in-memory score still stands.
        }
    }

    private static string GetFallbackPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        AppFolderName,
        FileName);

    private sealed class HighScoreData
    {
        public int HighScore { get; set; }
    }
}
