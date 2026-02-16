using System.Net.Http;
using Microsoft.Win32;
using ZaraGON.Core.Constants;
using ZaraGON.Core.Interfaces.Infrastructure;
using ZaraGON.Core.Models;
using SysDiag = System.Diagnostics;

namespace ZaraGON.Infrastructure.Runtime;

public sealed class VcRedistChecker : IVcRedistChecker
{
    private readonly HttpClient _httpClient;

    // Minimum VC++ runtime versions required per VS toolset
    private static readonly Dictionary<string, Version> VsVersionMinimums = new(StringComparer.OrdinalIgnoreCase)
    {
        ["vs16"] = new Version(14, 20, 0, 0),  // VS 2019
        ["vs17"] = new Version(14, 30, 0, 0),  // VS 2022
    };

    public VcRedistChecker(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<Version?> GetInstalledVersionAsync(CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            // Try registry first (most reliable)
            var regVersion = GetVersionFromRegistry();
            if (regVersion != null) return regVersion;

            // Fallback: check DLL file version
            return GetVersionFromDll();
        }, ct);
    }

    public async Task<VcRedistStatus> CheckCompatibilityAsync(string? vsVersion, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(vsVersion))
        {
            return new VcRedistStatus
            {
                IsCompatible = true,
                VsVersion = vsVersion,
                DownloadUrl = Defaults.VcRedistDownloadUrl
            };
        }

        var installed = await GetInstalledVersionAsync(ct);
        var requiredMin = VsVersionMinimums.GetValueOrDefault(vsVersion);

        if (requiredMin == null)
        {
            // Unknown VS version — assume compatible
            return new VcRedistStatus
            {
                IsCompatible = true,
                InstalledVersion = installed,
                VsVersion = vsVersion,
                DownloadUrl = Defaults.VcRedistDownloadUrl
            };
        }

        var isCompatible = installed != null && installed >= requiredMin;

        return new VcRedistStatus
        {
            IsCompatible = isCompatible,
            InstalledVersion = installed,
            RequiredMinimumVersion = requiredMin,
            VsVersion = vsVersion,
            DownloadUrl = Defaults.VcRedistDownloadUrl
        };
    }

    public async Task<VcRedistStatus> TestPhpBinaryAsync(string phpExePath, CancellationToken ct = default)
    {
        var installed = await GetInstalledVersionAsync(ct);

        if (!File.Exists(phpExePath))
        {
            return new VcRedistStatus
            {
                IsCompatible = true,
                InstalledVersion = installed,
                DownloadUrl = Defaults.VcRedistDownloadUrl
            };
        }

        try
        {
            var psi = new SysDiag.ProcessStartInfo
            {
                FileName = phpExePath,
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = SysDiag.Process.Start(psi);
            if (process == null)
            {
                return new VcRedistStatus
                {
                    IsCompatible = false,
                    InstalledVersion = installed,
                    DownloadUrl = Defaults.VcRedistDownloadUrl
                };
            }

            var stderr = await process.StandardError.ReadToEndAsync(ct);
            var stdout = await process.StandardOutput.ReadToEndAsync(ct);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(5000);
            try { await process.WaitForExitAsync(cts.Token); } catch (OperationCanceledException) { }

            // Check stderr for VCRUNTIME incompatibility
            if (stderr.Contains("VCRUNTIME", StringComparison.OrdinalIgnoreCase) &&
                stderr.Contains("not compatible", StringComparison.OrdinalIgnoreCase))
            {
                // Try to parse the required version from "linked with 14.44"
                Version? requiredVersion = null;
                var match = System.Text.RegularExpressions.Regex.Match(stderr, @"linked with (\d+\.\d+)");
                if (match.Success && Version.TryParse(match.Groups[1].Value, out var parsed))
                    requiredVersion = parsed;

                return new VcRedistStatus
                {
                    IsCompatible = false,
                    InstalledVersion = installed,
                    RequiredMinimumVersion = requiredVersion,
                    DownloadUrl = Defaults.VcRedistDownloadUrl
                };
            }

            // PHP ran fine
            return new VcRedistStatus
            {
                IsCompatible = true,
                InstalledVersion = installed,
                DownloadUrl = Defaults.VcRedistDownloadUrl
            };
        }
        catch
        {
            // If we can't run PHP, it might be a VCRUNTIME issue (DLL not found crash)
            return new VcRedistStatus
            {
                IsCompatible = false,
                InstalledVersion = installed,
                DownloadUrl = Defaults.VcRedistDownloadUrl
            };
        }
    }

    public async Task InstallAsync(IProgress<double>? progress = null, CancellationToken ct = default)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), Defaults.VcRedistFileName);

        try
        {
            // Download
            progress?.Report(0);

            using var response = await _httpClient.GetAsync(Defaults.VcRedistDownloadUrl,
                HttpCompletionOption.ResponseHeadersRead, ct);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            long bytesRead = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int read;
            while ((read = await contentStream.ReadAsync(buffer, ct)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read), ct);
                bytesRead += read;

                if (totalBytes > 0)
                    progress?.Report((double)bytesRead / totalBytes * 80); // 0-80% for download
            }

            progress?.Report(80);

            // Install silently (UAC will be shown by Windows via UseShellExecute)
            var psi = new SysDiag.ProcessStartInfo
            {
                FileName = tempPath,
                Arguments = "/install /quiet /norestart",
                UseShellExecute = true,
                Verb = "runas"
            };

            progress?.Report(85);

            using var process = SysDiag.Process.Start(psi);
            if (process != null)
            {
                await process.WaitForExitAsync(ct);

                if (process.ExitCode != 0 && process.ExitCode != 3010) // 3010 = success, reboot required
                    throw new InvalidOperationException(
                        $"VC++ Redistributable kurulumu başarısız oldu (çıkış kodu: {process.ExitCode})");
            }

            progress?.Report(100);
        }
        finally
        {
            // Cleanup temp file
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private static Version? GetVersionFromRegistry()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64");

            if (key == null) return null;

            var major = key.GetValue("Major") as int?;
            var minor = key.GetValue("Minor") as int?;
            var build = key.GetValue("Bld") as int?;

            if (major.HasValue && minor.HasValue && build.HasValue)
                return new Version(major.Value, minor.Value, build.Value, 0);
        }
        catch { }

        return null;
    }

    private static Version? GetVersionFromDll()
    {
        try
        {
            var systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            var dllPath = Path.Combine(systemDir, "vcruntime140.dll");

            if (!File.Exists(dllPath)) return null;

            var versionInfo = SysDiag.FileVersionInfo.GetVersionInfo(dllPath);
            return new Version(
                versionInfo.FileMajorPart,
                versionInfo.FileMinorPart,
                versionInfo.FileBuildPart,
                versionInfo.FilePrivatePart);
        }
        catch { }

        return null;
    }
}
