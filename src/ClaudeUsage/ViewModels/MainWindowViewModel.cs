using System.Collections.ObjectModel;
using Avalonia.Layout;
using Avalonia.Threading;
using ClaudeUsage.Models;
using ClaudeUsage.Services;

namespace ClaudeUsage.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    // After an HTTP 429 with no usable retry-after header, wait this long
    // before the next request; never wait longer than the maximum.
    private static readonly TimeSpan RateLimitWait = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan MaximumRateLimitWait = TimeSpan.FromHours(1);

    private readonly AppSettings _settings = AppSettings.Load();
    private readonly UsageCache _cache = UsageCache.Load();
    private readonly UsageClient _client = new();
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _refresh;

    private string _statusText = "Loading...";
    private bool _isBusy;
    private bool _isSettingsOpen;
    private Choice<string> _selectedDisplay;
    private Choice<string> _selectedTray;
    private Choice<int> _selectedRefresh;

    public MainWindowViewModel()
    {
        DisplayChoices =
        [
            new("Remaining", AppSettings.DisplayRemaining),
            new("Used", AppSettings.DisplayUsed),
        ];
        TrayChoices =
        [
            new("Current session", "session"),
            new("Current week (all models)", "weekly_all"),
            new("Current week (model limit)", "weekly_scoped"),
            new("Lowest remaining", "lowest"),
        ];
        RefreshChoices =
        [
            new("Manual only", AppSettings.ManualRefresh),
            new("Every 10 seconds", 10),
            new("Every 30 seconds", 30),
            new("Every minute", 60),
            new("Every 5 minutes", 300),
        ];

        // A value typed into settings.json by hand gets its own entry.
        if (RefreshChoices.All(choice => choice.Value != _settings.RefreshSeconds))
        {
            RefreshChoices.Add(new($"Every {_settings.RefreshSeconds} seconds", _settings.RefreshSeconds));
        }

        _selectedDisplay = DisplayChoices.FirstOrDefault(choice => choice.Value == _settings.DisplayMode) ?? DisplayChoices[0];
        _selectedTray = TrayChoices.FirstOrDefault(choice => choice.Value == _settings.TrayLimit) ?? TrayChoices[1];
        _selectedRefresh = RefreshChoices.First(choice => choice.Value == _settings.RefreshSeconds);

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) =>
        {
            var now = DateTimeOffset.Now;
            foreach (var card in Limits)
            {
                card.Tick(now);
            }
        };

        _refresh = new DispatcherTimer();
        _refresh.Tick += async (_, _) => await RefreshAsync();
    }

    public AppSettings Settings => _settings;

    // Raised after every fetch attempt, successful or not, and after a settings change.
    public event Action? Updated;

    public ObservableCollection<LimitCardViewModel> Limits { get; } = [];

    public List<Choice<string>> DisplayChoices { get; }
    public List<Choice<string>> TrayChoices { get; }
    public List<Choice<int>> RefreshChoices { get; }

    public Choice<string> SelectedDisplay
    {
        get => _selectedDisplay;
        set
        {
            if (value is null || !Set(ref _selectedDisplay, value))
            {
                return;
            }

            _settings.DisplayMode = value.Value;
            foreach (var card in Limits)
            {
                card.ShowUsed = _settings.ShowUsed;
            }

            SaveSettings();
        }
    }

    public Choice<string> SelectedTray
    {
        get => _selectedTray;
        set
        {
            if (value is null || !Set(ref _selectedTray, value))
            {
                return;
            }

            _settings.TrayLimit = value.Value;
            SaveSettings();
        }
    }

    public Choice<int> SelectedRefresh
    {
        get => _selectedRefresh;
        set
        {
            if (value is null || !Set(ref _selectedRefresh, value))
            {
                return;
            }

            _settings.RefreshSeconds = value.Value;
            ApplyRefreshInterval();
            SaveSettings();
        }
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => Set(ref _isSettingsOpen, value);
    }

    // True when the cards lie side by side, false when they are stacked.
    public bool IsHorizontal
    {
        get => _settings.IsHorizontal;
        set
        {
            if (value == _settings.IsHorizontal)
            {
                return;
            }

            _settings.Layout = value ? AppSettings.LayoutHorizontal : AppSettings.LayoutVertical;
            Raise(nameof(IsHorizontal));
            Raise(nameof(CardOrientation));
            SaveSettings();
        }
    }

    public Orientation CardOrientation => IsHorizontal ? Orientation.Horizontal : Orientation.Vertical;

    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set => Set(ref _isBusy, value);
    }

    public void Start()
    {
        if (_cache.Usage is { } cached)
        {
            ShowUsage(cached, DateTimeOffset.Now);
        }

        _clock.Start();
        ApplyRefreshInterval();
        _ = RefreshAsync();
    }

    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        // Still inside a server block: send nothing, as a request would only be refused.
        if (_cache.BlockedUntil is { } blockedUntil && DateTimeOffset.Now < blockedUntil)
        {
            StatusText = BlockedText(blockedUntil) + CachedSuffix();
            Updated?.Invoke();
            return;
        }

        IsBusy = true;
        try
        {
            var usage = await _client.FetchAsync();
            var now = DateTimeOffset.Now;

            _cache.Usage = usage;
            _cache.FetchedAt = now;
            _cache.BlockedUntil = null;
            _cache.Save();

            ShowUsage(usage, now);
            StatusText = Limits.Count == 0
                ? "The response contained no limits."
                : $"Updated {now:HH:mm:ss}";
        }
        catch (RateLimitedException ex)
        {
            var wait = ex.RetryAfter is { } retryAfter && retryAfter > TimeSpan.Zero ? retryAfter : RateLimitWait;
            if (wait > MaximumRateLimitWait)
            {
                wait = MaximumRateLimitWait;
            }

            var until = DateTimeOffset.Now + wait;
            _cache.BlockedUntil = until;
            _cache.Save();
            StatusText = BlockedText(until) + CachedSuffix();
        }
        catch (Exception ex) when (ex is UsageException or HttpRequestException or TaskCanceledException or IOException or System.Text.Json.JsonException)
        {
            StatusText = ex.Message + CachedSuffix();
        }
        finally
        {
            IsBusy = false;
            Updated?.Invoke();
        }
    }

    private void ShowUsage(UsageResponse usage, DateTimeOffset now)
    {
        Limits.Clear();
        foreach (var entry in usage.Limits ?? [])
        {
            Limits.Add(new LimitCardViewModel(entry, now, _settings.ShowUsed, usage.SevenDayBreakdown?.WindowStartedAt));
        }
    }

    private static string BlockedText(DateTimeOffset until) =>
        $"HTTP 429: rate limited by the server. Next try at {until.ToLocalTime():HH:mm:ss}.";

    private string CachedSuffix() => Limits.Count > 0 && _cache.FetchedAt is { } fetchedAt
        ? $" Showing values fetched {fetchedAt.ToLocalTime():dd/MM HH:mm:ss}."
        : "";

    private void ApplyRefreshInterval()
    {
        _refresh.Stop();
        if (_settings.RefreshInterval is { } interval)
        {
            _refresh.Interval = interval;
            _refresh.Start();
        }
    }

    private void SaveSettings()
    {
        if (!_settings.Save())
        {
            StatusText = $"Could not write {AppSettings.FilePath}";
        }

        Updated?.Invoke();
    }
}
