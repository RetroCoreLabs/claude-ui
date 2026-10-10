using System.Text.Json;
using System.Text.Json.Serialization;
using ClaudeUsage.Models;

namespace ClaudeUsage.Services;

// The last good reply and the rate-limit state, kept on disk so that a start
// during a server block shows the last known values and sends no request.
public sealed class UsageCache
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    [JsonPropertyName("fetchedAt")]
    public DateTimeOffset? FetchedAt { get; set; }

    // Set after an HTTP 429. No request is sent before this time.
    [JsonPropertyName("blockedUntil")]
    public DateTimeOffset? BlockedUntil { get; set; }

    [JsonPropertyName("usage")]
    public UsageResponse? Usage { get; set; }

    // last-usage.json next to settings.json.
    public static string FilePath => Path.Combine(
        Path.GetDirectoryName(AppSettings.FilePath)!,
        "last-usage.json");

    public static UsageCache Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                return JsonSerializer.Deserialize<UsageCache>(File.ReadAllText(FilePath)) ?? new UsageCache();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
        }

        return new UsageCache();
    }

    // Writes the file. A failure is ignored: the cache is a convenience only.
    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, WriteOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
