using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeUsage.Services;

public sealed class AppSettings
{
    public const int DefaultRefreshSeconds = 300;
    public const int ManualRefresh = 0;
    private const int MinimumRefreshSeconds = 5;

    public const string DisplayRemaining = "remaining";
    public const string DisplayUsed = "used";

    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    // Seconds between fetches. 0 means fetch only when Refresh is pressed.
    [JsonPropertyName("refreshSeconds")]
    public int RefreshSeconds { get; set; } = DefaultRefreshSeconds;

    // Which limit the tray icon number shows: the "kind" of a limit in the
    // usage response ("session", "weekly_all", "weekly_scoped"), or "lowest"
    // for whichever limit has the least remaining.
    [JsonPropertyName("trayLimit")]
    public string TrayLimit { get; set; } = "weekly_all";

    // Whether the cards and the tray icon show the percent that is left
    // ("remaining") or the percent that is spent ("used").
    [JsonPropertyName("displayMode")]
    public string DisplayMode { get; set; } = DisplayRemaining;

    [JsonIgnore]
    public bool ShowUsed => DisplayMode == DisplayUsed;

    // Null when refresh is manual only.
    [JsonIgnore]
    public TimeSpan? RefreshInterval => RefreshSeconds <= ManualRefresh
        ? null
        : TimeSpan.FromSeconds(Math.Max(MinimumRefreshSeconds, RefreshSeconds));

    // settings.json in the per-user application data folder, in a ClaudeUsage subfolder.
    public static string FilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ClaudeUsage",
        "settings.json");

    // Reads the settings file. Writes one with the defaults if none exists,
    // so there is a file to edit. Falls back to the defaults on any error.
    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new AppSettings();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppSettings();
        }

        var defaults = new AppSettings();
        defaults.Save();
        return defaults;
    }

    // Writes the settings file. Returns false if the file could not be written.
    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, WriteOptions));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }
}
