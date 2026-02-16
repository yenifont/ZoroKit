namespace ZaraGON.Core.Models;

public sealed class PhpSettings
{
    public string MemoryLimit { get; set; } = "256M";
    public string UploadMaxFilesize { get; set; } = "128M";
    public string PostMaxSize { get; set; } = "128M";
    public int MaxExecutionTime { get; set; } = 300;
    public int MaxInputTime { get; set; } = 300;
    public int MaxFileUploads { get; set; } = 20;
    public int MaxInputVars { get; set; } = 1000;
    public bool DisplayErrors { get; set; } = true;
    public string ErrorReporting { get; set; } = "E_ALL";
    public string DateTimezone { get; set; } = "UTC";
}
