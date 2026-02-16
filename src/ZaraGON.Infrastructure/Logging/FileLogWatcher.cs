using ZaraGON.Core.Interfaces.Services;
using ZaraGON.Core.Models;

namespace ZaraGON.Infrastructure.Logging;

public sealed class FileLogWatcher : ILogWatcher, IDisposable
{
    private readonly Dictionary<string, WatcherState> _watchers = [];
    private readonly List<LogEntry> _entries = [];
    private readonly object _lock = new();

    private const int MaxEntries = 10000;
    private const int TrimTarget = 5000;

    public event EventHandler<LogEntry>? LogReceived;

    public Task StartWatchingAsync(string filePath, string source, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_watchers.ContainsKey(filePath))
                return Task.CompletedTask;

            var dir = Path.GetDirectoryName(filePath)!;
            var fileName = Path.GetFileName(filePath);

            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            // Read initial position
            long initialPosition = 0;
            if (File.Exists(filePath))
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                initialPosition = fs.Length;
            }

            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var state = new WatcherState
            {
                FilePath = filePath,
                Source = source,
                LastPosition = initialPosition,
                CancellationSource = cts
            };

            // Store read callback for debounced reads
            state.ReadCallback = () => ReadNewLines(state);

            // Fallback timer with longer interval (FileSystemWatcher handles immediate detection)
            state.Timer = new Timer(_ => ReadNewLines(state), null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3));

            // FileSystemWatcher for immediate change detection with debounce
            if (Directory.Exists(dir))
            {
                var watcher = new FileSystemWatcher(dir, fileName)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                watcher.Changed += (_, _) => state.DebouncedRead();
                state.FileWatcher = watcher;
            }

            _watchers[filePath] = state;
        }

        return Task.CompletedTask;
    }

    public Task StopWatchingAsync(string filePath)
    {
        lock (_lock)
        {
            if (_watchers.Remove(filePath, out var state))
            {
                state.Dispose();
            }
        }
        return Task.CompletedTask;
    }

    public Task StopAllAsync()
    {
        lock (_lock)
        {
            foreach (var state in _watchers.Values)
                state.Dispose();
            _watchers.Clear();
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<LogEntry>> GetRecentEntriesAsync(string source, int count = 100, CancellationToken ct = default)
    {
        lock (_lock)
        {
            var filtered = _entries
                .Where(e => string.IsNullOrEmpty(source) || e.Source.Equals(source, StringComparison.OrdinalIgnoreCase))
                .TakeLast(count)
                .ToList();
            return Task.FromResult<IReadOnlyList<LogEntry>>(filtered);
        }
    }

    private void ReadNewLines(WatcherState state)
    {
        try
        {
            if (!File.Exists(state.FilePath))
                return;

            using var fs = new FileStream(state.FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length < state.LastPosition)
                state.LastPosition = 0; // File was truncated

            if (fs.Length <= state.LastPosition)
                return;

            fs.Seek(state.LastPosition, SeekOrigin.Begin);
            using var reader = new StreamReader(fs);

            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                var entry = ParseLogLine(line, state.Source, state.FilePath);

                lock (_lock)
                {
                    _entries.Add(entry);
                    if (_entries.Count > MaxEntries)
                        _entries.RemoveRange(0, _entries.Count - TrimTarget);
                }

                LogReceived?.Invoke(this, entry);
            }

            state.LastPosition = fs.Position;
        }
        catch
        {
            // File may be locked
        }
    }

    private static LogEntry ParseLogLine(string line, string source, string filePath)
    {
        var level = "INFO";
        var span = line.AsSpan();
        if (span.Contains("[error]", StringComparison.OrdinalIgnoreCase) || span.Contains(":error]", StringComparison.Ordinal))
            level = "ERROR";
        else if (span.Contains("[warn]", StringComparison.OrdinalIgnoreCase) || span.Contains(":warn]", StringComparison.Ordinal))
            level = "WARN";
        else if (span.Contains("[notice]", StringComparison.OrdinalIgnoreCase) || span.Contains(":notice]", StringComparison.Ordinal))
            level = "NOTICE";

        return new LogEntry
        {
            Timestamp = DateTime.Now,
            Source = source,
            Level = level,
            Message = line,
            FilePath = filePath
        };
    }

    public void Dispose()
    {
        StopAllAsync().GetAwaiter().GetResult();
    }

    private sealed class WatcherState : IDisposable
    {
        public required string FilePath { get; init; }
        public required string Source { get; init; }
        public long LastPosition { get; set; }
        public Timer? Timer { get; set; }
        public FileSystemWatcher? FileWatcher { get; set; }
        public required CancellationTokenSource CancellationSource { get; init; }

        private Timer? _debounceTimer;
        private readonly object _debounceLock = new();

        public Action? ReadCallback { get; set; }

        /// <summary>
        /// Debounce FileSystemWatcher events — coalesce rapid-fire notifications into a single read.
        /// </summary>
        public void DebouncedRead()
        {
            lock (_debounceLock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(_ => ReadCallback?.Invoke(), null, 150, Timeout.Infinite);
            }
        }

        public void Dispose()
        {
            Timer?.Dispose();
            FileWatcher?.Dispose();
            _debounceTimer?.Dispose();
            CancellationSource.Cancel();
            CancellationSource.Dispose();
        }
    }
}
