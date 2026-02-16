namespace ZaraGON.Core.Models;

public sealed class QuickFix
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IconKind { get; set; } = "AutoFix";
    public string Category { get; set; } = string.Empty;
    public bool IsDetected { get; set; }
    public string DetectionDetail { get; set; } = string.Empty;
}
