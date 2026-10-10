using Avalonia;

namespace ClaudeUsage;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            // Linux only (ignored elsewhere): the window class the desktop matches against
            // claude-usage.desktop's StartupWMClass to pick the menu icon for the taskbar and dock.
            .With(new X11PlatformOptions { WmClass = "claude-usage" })
            .WithInterFont()
            .LogToTrace();
}
