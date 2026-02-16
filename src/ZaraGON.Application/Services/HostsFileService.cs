using ZaraGON.Core.Constants;
using ZaraGON.Core.Exceptions;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Application.Services;

public sealed class HostsFileService : IHostsFileManager
{
    private readonly IFileSystem _fileSystem;
    private readonly IPrivilegeManager _privilegeManager;

    public HostsFileService(IFileSystem fileSystem, IPrivilegeManager privilegeManager)
    {
        _fileSystem = fileSystem;
        _privilegeManager = privilegeManager;
    }

    public async Task<IReadOnlyList<HostEntry>> GetManagedEntriesAsync(CancellationToken ct = default)
    {
        if (!_fileSystem.FileExists(HostsFileMarkers.HostsFilePath))
            return [];

        var content = await _fileSystem.ReadAllTextAsync(HostsFileMarkers.HostsFilePath, ct);
        return ParseManagedEntries(content);
    }

    public async Task AddEntryAsync(HostEntry entry, CancellationToken ct = default)
    {
        var entries = (await GetManagedEntriesAsync(ct)).ToList();

        // Remove existing entry with same hostname
        entries.RemoveAll(e => string.Equals(e.Hostname, entry.Hostname, StringComparison.OrdinalIgnoreCase));
        entries.Add(entry);

        await WriteManagedEntriesAsync(entries, ct);
    }

    public async Task RemoveEntryAsync(string hostname, CancellationToken ct = default)
    {
        var entries = (await GetManagedEntriesAsync(ct)).ToList();
        entries.RemoveAll(e => string.Equals(e.Hostname, hostname, StringComparison.OrdinalIgnoreCase));
        await WriteManagedEntriesAsync(entries, ct);
    }

    public async Task SyncEntriesAsync(IEnumerable<HostEntry> entries, CancellationToken ct = default)
    {
        await WriteManagedEntriesAsync(entries.ToList(), ct);
    }

    public Task<bool> RequiresElevationAsync(CancellationToken ct = default)
    {
        return Task.FromResult(!_privilegeManager.IsRunningAsAdmin());
    }

    private async Task WriteManagedEntriesAsync(List<HostEntry> entries, CancellationToken ct)
    {
        if (!_fileSystem.FileExists(HostsFileMarkers.HostsFilePath))
            throw new HostsFileException("Hosts file not found.");

        var content = await _fileSystem.ReadAllTextAsync(HostsFileMarkers.HostsFilePath, ct);

        // Remove existing ZaraGON section
        var lines = content.Split('\n').ToList();
        int startIdx = -1, endIdx = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].TrimEnd('\r').Trim() == HostsFileMarkers.StartMarker)
                startIdx = i;
            if (lines[i].TrimEnd('\r').Trim() == HostsFileMarkers.EndMarker)
                endIdx = i;
        }

        if (startIdx >= 0 && endIdx >= startIdx)
        {
            lines.RemoveRange(startIdx, endIdx - startIdx + 1);
        }

        // Remove trailing empty lines
        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
            lines.RemoveAt(lines.Count - 1);

        // Add ZaraGON section
        if (entries.Count > 0)
        {
            lines.Add("");
            lines.Add(HostsFileMarkers.StartMarker);
            foreach (var entry in entries)
            {
                var line = $"{entry.IpAddress}\t{entry.Hostname}";
                if (!string.IsNullOrEmpty(entry.Comment))
                    line += $"\t# {entry.Comment}";
                lines.Add(line);
            }
            lines.Add(HostsFileMarkers.EndMarker);
        }

        var newContent = string.Join(Environment.NewLine, lines) + Environment.NewLine;

        try
        {
            await _fileSystem.AtomicWriteAsync(HostsFileMarkers.HostsFilePath, newContent, ct);
        }
        catch (UnauthorizedAccessException)
        {
            throw new HostsFileException("Administrator privileges required to modify the hosts file.");
        }
    }

    private static List<HostEntry> ParseManagedEntries(string content)
    {
        var entries = new List<HostEntry>();
        var lines = content.Split('\n');
        var inSection = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r').Trim();

            if (line == HostsFileMarkers.StartMarker) { inSection = true; continue; }
            if (line == HostsFileMarkers.EndMarker) break;

            if (!inSection || string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
                continue;

            var parts = line.Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;

            var comment = line.Contains('#') ? line[(line.IndexOf('#') + 1)..].Trim() : null;

            entries.Add(new HostEntry
            {
                IpAddress = parts[0],
                Hostname = parts[1],
                Comment = comment,
                IsEnabled = true
            });
        }

        return entries;
    }
}
