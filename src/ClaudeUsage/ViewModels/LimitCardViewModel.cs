using System.Globalization;
using Avalonia.Media;
using ClaudeUsage.Models;

namespace ClaudeUsage.ViewModels;

public sealed class LimitCardViewModel : ViewModelBase
{
    private static readonly IBrush Green = new SolidColorBrush(Color.Parse("#34D399"));
    private static readonly IBrush Amber = new SolidColorBrush(Color.Parse("#FBBF24"));
    private static readonly IBrush Red = new SolidColorBrush(Color.Parse("#F87171"));

    private readonly DateTimeOffset? _resetsAt;
    private readonly TimeSpan? _window;
    private bool _showUsed;

    // weekStartedAt is seven_day_breakdown.window_started_at from the response, if present.
    public LimitCardViewModel(LimitEntry entry, DateTimeOffset now, bool showUsed, DateTimeOffset? weekStartedAt)
    {
        _resetsAt = entry.ResetsAt;
        _window = WindowFor(entry.Group, entry.ResetsAt, weekStartedAt);
        _showUsed = showUsed;

        Kind = entry.Kind ?? "";
        Title = TitleFor(entry);
        UsedPercent = Math.Clamp(entry.Percent, 0, 100);
        RemainingPercent = 100 - UsedPercent;
        AccentBrush = entry.Severity switch
        {
            "normal" => Green,
            "critical" => Red,
            _ => Amber,
        };
        ResetText = _resetsAt is { } resetsAt
            ? "Reset: " + resetsAt.ToLocalTime().ToString("dd/MM HH:mm", CultureInfo.InvariantCulture)
            : "Reset: -";

        Tick(now);
    }

    public string Kind { get; }
    public string Title { get; }
    public double UsedPercent { get; }
    public double RemainingPercent { get; }
    public IBrush AccentBrush { get; }
    public string ResetText { get; }

    // True shows the used percent as the big number and bar, false the remaining percent.
    public bool ShowUsed
    {
        get => _showUsed;
        set
        {
            if (Set(ref _showUsed, value))
            {
                Raise(nameof(PrimaryLabel));
                Raise(nameof(PrimaryPercent));
                Raise(nameof(PrimaryText));
                Raise(nameof(SecondaryText));
                Raise(nameof(ShortText));
            }
        }
    }

    public string PrimaryLabel => _showUsed ? "Used" : "Remaining";
    public double PrimaryPercent => _showUsed ? UsedPercent : RemainingPercent;
    public string PrimaryText => $"{PrimaryPercent:0}%";
    public string SecondaryText => _showUsed ? $"Remaining {RemainingPercent:0}%" : $"Used {UsedPercent:0}%";

    // One line for the tray tooltip.
    public string ShortText => _showUsed ? $"{Title}: {UsedPercent:0}% used" : $"{Title}: {RemainingPercent:0}% left";

    public double TimeLeftPercent { get; private set; }
    public string TimeLeftText { get; private set; } = "-";
    public string CountdownText { get; private set; } = "";

    // Recomputes the values that change as the clock moves.
    public void Tick(DateTimeOffset now)
    {
        if (_resetsAt is not { } resetsAt)
        {
            return;
        }

        var left = resetsAt - now;
        if (left < TimeSpan.Zero)
        {
            left = TimeSpan.Zero;
        }

        if (_window is { } window)
        {
            TimeLeftPercent = Math.Clamp(left / window * 100, 0, 100);
            TimeLeftText = $"{TimeLeftPercent:0}%";
        }

        CountdownText = left == TimeSpan.Zero ? "Resetting now" : "Resets in " + Format(left);

        Raise(nameof(TimeLeftPercent));
        Raise(nameof(TimeLeftText));
        Raise(nameof(CountdownText));
    }

    private static string TitleFor(LimitEntry entry) => entry.Kind switch
    {
        "session" => "Current session",
        "weekly_all" => "Current week (all models)",
        "weekly_scoped" => $"Current week ({entry.Scope?.Model?.DisplayName ?? "scoped"})",
        _ => entry.Kind ?? "Unknown limit",
    };

    // Length of the limit's time window; time left is shown as a percent of it.
    // Weekly: from the window start in the response to resets_at, or 7 days
    // if the response has no start. Session: the response has no start time,
    // so 5 hours is taken from the response field name "five_hour".
    private static TimeSpan? WindowFor(string? group, DateTimeOffset? resetsAt, DateTimeOffset? weekStartedAt)
    {
        switch (group)
        {
            case "session":
                return TimeSpan.FromHours(5);
            case "weekly":
                if (resetsAt is { } reset && weekStartedAt is { } start && reset > start)
                {
                    return reset - start;
                }

                return TimeSpan.FromDays(7);
            default:
                return null;
        }
    }

    private static string Format(TimeSpan left)
    {
        if (left.TotalDays >= 1)
        {
            return $"{(int)left.TotalDays}d {left.Hours}h";
        }

        if (left.TotalHours >= 1)
        {
            return $"{(int)left.TotalHours}h {left.Minutes:00}m";
        }

        return $"{left.Minutes}m {left.Seconds:00}s";
    }
}
