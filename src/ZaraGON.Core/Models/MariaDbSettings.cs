namespace ZaraGON.Core.Models;

public sealed class MariaDbSettings
{
    public string InnodbBufferPoolSize { get; set; } = "128M";
    public int MaxConnections { get; set; } = 151;
    public string MaxAllowedPacket { get; set; } = "16M";
}
