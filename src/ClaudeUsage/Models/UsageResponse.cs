using System.Text.Json.Serialization;

namespace ClaudeUsage.Models;

// Only the fields the UI draws are mapped. The response carries many more.
public sealed class UsageResponse
{
    [JsonPropertyName("limits")]
    public List<LimitEntry>? Limits { get; set; }

    [JsonPropertyName("seven_day_breakdown")]
    public SevenDayBreakdown? SevenDayBreakdown { get; set; }
}

public sealed class SevenDayBreakdown
{
    // Start of the current weekly window.
    [JsonPropertyName("window_started_at")]
    public DateTimeOffset? WindowStartedAt { get; set; }
}

public sealed class LimitEntry
{
    // Seen in a live response: "session", "weekly_all", "weekly_scoped".
    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    // Seen in a live response: "session", "weekly".
    [JsonPropertyName("group")]
    public string? Group { get; set; }

    // Percent of the limit that is used, 0..100.
    [JsonPropertyName("percent")]
    public double Percent { get; set; }

    // Seen in a live response: "normal", "critical".
    [JsonPropertyName("severity")]
    public string? Severity { get; set; }

    [JsonPropertyName("resets_at")]
    public DateTimeOffset? ResetsAt { get; set; }

    [JsonPropertyName("scope")]
    public LimitScope? Scope { get; set; }
}

public sealed class LimitScope
{
    [JsonPropertyName("model")]
    public ScopeModel? Model { get; set; }
}

public sealed class ScopeModel
{
    [JsonPropertyName("display_name")]
    public string? DisplayName { get; set; }
}
