namespace ZaraGON.Core.Exceptions;

public class ZaraGONException : Exception
{
    public ZaraGONException(string message) : base(message) { }
    public ZaraGONException(string message, Exception innerException) : base(message, innerException) { }
}

public class ServiceStartException : ZaraGONException
{
    public string? ServiceName { get; }
    public ServiceStartException(string serviceName, string message) : base(message) => ServiceName = serviceName;
    public ServiceStartException(string serviceName, string message, Exception inner) : base(message, inner) => ServiceName = serviceName;
}

public class PortConflictException : ZaraGONException
{
    public int Port { get; }
    public int? ConflictingPid { get; }
    public PortConflictException(int port, int? pid = null) : base($"Port {port} zaten kullanımda" + (pid.HasValue ? $" (PID: {pid})" : ""))
    {
        Port = port;
        ConflictingPid = pid;
    }
}

public class VersionNotFoundException : ZaraGONException
{
    public string? Version { get; }
    public VersionNotFoundException(string version) : base($"Version '{version}' not found") => Version = version;
}

public class DownloadFailedException : ZaraGONException
{
    public string? Url { get; }
    public DownloadFailedException(string url, string message) : base(message) => Url = url;
    public DownloadFailedException(string url, string message, Exception inner) : base(message, inner) => Url = url;
}

public class ConfigurationException : ZaraGONException
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception inner) : base(message, inner) { }
}

public class HostsFileException : ZaraGONException
{
    public HostsFileException(string message) : base(message) { }
    public HostsFileException(string message, Exception inner) : base(message, inner) { }
}

public class PrivilegeException : ZaraGONException
{
    public PrivilegeException(string message) : base(message) { }
}
