using ZaraGON.Core.Interfaces.Infrastructure;

namespace ZaraGON.Infrastructure.FileSystem;

public sealed class WindowsFileSystem : IFileSystem
{
    public bool FileExists(string path) => File.Exists(path);

    public bool DirectoryExists(string path) => Directory.Exists(path);

    public async Task<string> ReadAllTextAsync(string path, CancellationToken ct = default)
        => await File.ReadAllTextAsync(path, ct);

    public async Task WriteAllTextAsync(string path, string content, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        await File.WriteAllTextAsync(path, content, ct);
    }

    public async Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default)
        => await File.ReadAllBytesAsync(path, ct);

    public void CreateDirectory(string path)
    {
        if (!Directory.Exists(path))
            Directory.CreateDirectory(path);
    }

    public void DeleteDirectory(string path, bool recursive = false)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, recursive);
    }

    public void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    public void CopyFile(string source, string destination, bool overwrite = false)
    {
        var dir = Path.GetDirectoryName(destination);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.Copy(source, destination, overwrite);
    }

    public void MoveFile(string source, string destination)
    {
        var dir = Path.GetDirectoryName(destination);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        File.Move(source, destination, overwrite: true);
    }

    public string[] GetFiles(string directory, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly)
        => Directory.Exists(directory) ? Directory.GetFiles(directory, searchPattern, searchOption) : [];

    public string[] GetDirectories(string directory)
        => Directory.Exists(directory) ? Directory.GetDirectories(directory) : [];

    public string CombinePath(params string[] paths) => Path.Combine(paths);

    public string GetFullPath(string path) => Path.GetFullPath(path);

    public long GetFileSize(string path) => new FileInfo(path).Length;

    public async Task AtomicWriteAsync(string path, string content, CancellationToken ct = default)
    {
        var dir = Path.GetDirectoryName(path)!;
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var tempPath = Path.Combine(dir, $".tmp_{Guid.NewGuid():N}");
        try
        {
            await File.WriteAllTextAsync(tempPath, content, ct);
            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
