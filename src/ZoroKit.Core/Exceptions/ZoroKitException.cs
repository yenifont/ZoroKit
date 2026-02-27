namespace ZoroKit.Core.Exceptions;

public class ZoroKitException : Exception
{
    public ZoroKitException(string message) : base(message) { }
    public ZoroKitException(string message, Exception innerException) : base(message, innerException) { }
}

public class ServiceStartException : ZoroKitException
{
    public string? ServiceName { get; }
    public ServiceStartException(string serviceName, string message) : base(message) => ServiceName = serviceName;
    public ServiceStartException(string serviceName, string message, Exception inner) : base(message, inner) => ServiceName = serviceName;
}

public class PortConflictException : ZoroKitException
{
    public int Port { get; }
    public int? ConflictingPid { get; }
    public PortConflictException(int port, int? pid = null) : base($"Port {port} zaten kullanımda" + (pid.HasValue ? $" (PID: {pid})" : ""))
    {
        Port = port;
        ConflictingPid = pid;
    }
}

public class VersionNotFoundException : ZoroKitException
{
    public string? Version { get; }
    public VersionNotFoundException(string version) : base($"Version '{version}' not found") => Version = version;
}

public class DownloadFailedException : ZoroKitException
{
    public string? Url { get; }
    public DownloadFailedException(string url, string message) : base(message) => Url = url;
    public DownloadFailedException(string url, string message, Exception inner) : base(message, inner) => Url = url;
}

public class ConfigurationException : ZoroKitException
{
    public ConfigurationException(string message) : base(message) { }
    public ConfigurationException(string message, Exception inner) : base(message, inner) { }
}

public class HostsFileException : ZoroKitException
{
    public HostsFileException(string message) : base(message) { }
    public HostsFileException(string message, Exception inner) : base(message, inner) { }
}

public class PrivilegeException : ZoroKitException
{
    public PrivilegeException(string message) : base(message) { }
}
