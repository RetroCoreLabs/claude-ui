namespace ClaudeUsage.ViewModels;

// One entry in a settings drop-down. ToString is what the drop-down displays.
public sealed record Choice<T>(string Label, T Value)
{
    public override string ToString() => Label;
}
