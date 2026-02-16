namespace ZaraGON.Core.Interfaces.Infrastructure;

public interface IFileSystem
{
    bool FileExists(string path);
    bool DirectoryExists(string path);
    Task<string> ReadAllTextAsync(string path, CancellationToken ct = default);
    Task WriteAllTextAsync(string path, string content, CancellationToken ct = default);
    Task<byte[]> ReadAllBytesAsync(string path, CancellationToken ct = default);
    void CreateDirectory(string path);
    void DeleteDirectory(string path, bool recursive = false);
    void DeleteFile(string path);
    void CopyFile(string source, string destination, bool overwrite = false);
    void MoveFile(string source, string destination);
    string[] GetFiles(string directory, string searchPattern = "*", SearchOption searchOption = SearchOption.TopDirectoryOnly);
    string[] GetDirectories(string directory);
    string CombinePath(params string[] paths);
    string GetFullPath(string path);
    long GetFileSize(string path);
    Task AtomicWriteAsync(string path, string content, CancellationToken ct = default);
}
