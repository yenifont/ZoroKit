namespace ZaraGON.Core.Models;

public sealed class PhpExtension
{
    public required string Name { get; init; }
    public string? DllFileName { get; init; }
    public bool IsEnabled { get; set; }
    public bool DllExists { get; init; }
    public bool IsBuiltIn { get; init; }
}
