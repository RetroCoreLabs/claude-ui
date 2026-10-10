using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

namespace ClaudeUsage.Services;

// Draws the tray icon: a rounded square in the given colour with a short text on it.
public static class TrayIconRenderer
{
    private const int Size = 64;

    private static readonly IBrush TextBrush = new SolidColorBrush(Color.Parse("#0B1020"));
    private static readonly Typeface Bold = new(FontFamily.Default, FontStyle.Normal, FontWeight.Bold);

    public static WindowIcon Render(string text, IBrush background)
    {
        var bitmap = new RenderTargetBitmap(new PixelSize(Size, Size));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawRectangle(background, null, new Rect(0, 0, Size, Size), 14, 14);

            var fontSize = text.Length >= 3 ? 32 : 44;
            var formatted = new FormattedText(
                text,
                CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight,
                Bold,
                fontSize,
                TextBrush);

            var origin = new Point((Size - formatted.Width) / 2, (Size - formatted.Height) / 2);
            context.DrawText(formatted, origin);
        }

        return new WindowIcon(bitmap);
    }
}
