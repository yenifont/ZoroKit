namespace ZoroKit.Core.Models;

public sealed class HostEntry
{
    public required string IpAddress { get; set; }
    public required string Hostname { get; set; }
    public string? Comment { get; set; }
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// VHost conf dosyasından okunan alt klasör adı (örn. "zoro").
    /// Hosts dosyasına yazılmaz, sadece UI gösterimi için kullanılır.
    /// </summary>
    public string? SubFolder { get; set; }

    public string? SubFolderDisplay => string.IsNullOrEmpty(SubFolder) ? null : $"www/{SubFolder}";
}
