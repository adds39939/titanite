namespace Titanite.UI.Services.Presentation;

public sealed record StatusMessage(string Text, StatusTone Tone)
{
    public static StatusMessage Success(string text) => new(text, StatusTone.Success);

    public static StatusMessage Warning(string text) => new(text, StatusTone.Warning);

    public static StatusMessage Error(string text) => new(text, StatusTone.Error);

    public string CssClass => Tone switch
    {
        StatusTone.Error => "status-error",
        StatusTone.Warning => "status-warning",
        _ => "status-success"
    };
}

public enum StatusTone
{
    Success,
    Warning,
    Error
}
