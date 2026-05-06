using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReplayFilesViewApi.Models;

namespace ReplayFilesViewApi.Services;

public interface ISystemService
{
    Task<(bool success, string output)> RestartGame(ProjectSettings project);
    IAsyncEnumerable<string> StreamLogs(ProjectSettings project, CancellationToken ct);
}

public class SystemService : ISystemService
{
    private readonly ILogger<SystemService> _logger;

    public SystemService(ILogger<SystemService> logger)
    {
        _logger = logger;
    }

    public async Task<(bool success, string output)> RestartGame(ProjectSettings project)
    {
        if (project.RestartType == RestartType.None || string.IsNullOrEmpty(project.RestartServiceName))
            return (false, "No restart configuration provided.");

        string command;
        string args;

        if (project.RestartType == RestartType.Systemd)
        {
            command = "sudo";
            args = $"systemctl restart {project.RestartServiceName}";
        }
        else // Docker
        {
            command = "docker";
            args = $"restart {project.RestartServiceName}";
        }

        return await ExecuteCommand(command, args);
    }

    public async IAsyncEnumerable<string> StreamLogs(ProjectSettings project, [EnumeratorCancellation] CancellationToken ct)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        if (project.RestartType == RestartType.Systemd)
        {
            process.StartInfo.FileName = "sudo";
            var unit = NormalizeSystemdUnit(project.RestartServiceName);
            var activeSince = await GetSystemdActiveEnterTimestampAsync(unit, ct);
            process.StartInfo.ArgumentList.Add("journalctl");
            process.StartInfo.ArgumentList.Add("-u");
            process.StartInfo.ArgumentList.Add(unit);
            process.StartInfo.ArgumentList.Add("-f");
            if (!string.IsNullOrWhiteSpace(activeSince))
            {
                process.StartInfo.ArgumentList.Add("--since");
                process.StartInfo.ArgumentList.Add(activeSince.Trim());
            }
            else
            {
                _logger.LogWarning("ActiveEnterTimestamp missing for unit {Unit}; streaming last 200 lines only", unit);
                process.StartInfo.ArgumentList.Add("-n");
                process.StartInfo.ArgumentList.Add("200");
            }
        }
        else // Docker
        {
            process.StartInfo.FileName = "docker";
            if (project.LogType == LogType.File && !string.IsNullOrEmpty(project.LogPath))
            {
                process.StartInfo.ArgumentList.Add("exec");
                process.StartInfo.ArgumentList.Add(project.RestartServiceName);
                process.StartInfo.ArgumentList.Add("tail");
                process.StartInfo.ArgumentList.Add("-f");
                process.StartInfo.ArgumentList.Add(project.LogPath);
            }
            else
            {
                var startedAt = await GetDockerContainerStartedAtAsync(project.RestartServiceName, ct);
                process.StartInfo.ArgumentList.Add("logs");
                process.StartInfo.ArgumentList.Add("-f");
                if (!string.IsNullOrWhiteSpace(startedAt))
                {
                    process.StartInfo.ArgumentList.Add("--since");
                    process.StartInfo.ArgumentList.Add(startedAt.Trim());
                }
                else
                {
                    _logger.LogWarning("Could not read StartedAt for container {Container}; using --tail 500", project.RestartServiceName);
                    process.StartInfo.ArgumentList.Add("--tail");
                    process.StartInfo.ArgumentList.Add("500");
                }

                process.StartInfo.ArgumentList.Add(project.RestartServiceName);
            }
        }

        if (process.Start())
        {
            _logger.LogInformation(
                "Started log stream for {Slug}: {FileName} {Args}",
                project.Slug,
                process.StartInfo.FileName,
                string.Join(" ", process.StartInfo.ArgumentList));

            var reader = process.StandardOutput;
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (line != null) yield return line;
            }

            if (!process.HasExited)
                process.Kill();
        }
        else
        {
            yield return "Failed to start log stream process.";
        }
    }

    private static string NormalizeSystemdUnit(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return name;
        if (name.Contains('.')) return name;
        return name + ".service";
    }

    private async Task<string?> GetDockerContainerStartedAtAsync(string containerName, CancellationToken ct)
    {
        var (ok, stdout) = await RunOnceAsync(
            "docker",
            ["inspect", "-f", "{{.State.StartedAt}}", containerName],
            ct);
        if (!ok) return null;
        var s = stdout.Trim();
        if (string.IsNullOrEmpty(s)) return null;
        // Created but never started
        if (s.StartsWith("0001-01-01", StringComparison.Ordinal)) return null;
        return s;
    }

    private async Task<string?> GetSystemdActiveEnterTimestampAsync(string unit, CancellationToken ct)
    {
        var (ok, stdout) = await RunOnceAsync(
            "sudo",
            ["systemctl", "show", unit, "-p", "ActiveEnterTimestamp", "--value"],
            ct);
        if (!ok) return null;
        var s = stdout.Trim();
        if (string.IsNullOrEmpty(s) || s.Equals("n/a", StringComparison.OrdinalIgnoreCase))
            return null;
        return s;
    }

    private async Task<(bool success, string output)> RunOnceAsync(string fileName, string[] arguments, CancellationToken ct)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };
            foreach (var a in arguments)
                process.StartInfo.ArgumentList.Add(a);

            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(ct);
            var errorTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct);
            var stdout = await outputTask;
            var stderr = await errorTask;

            if (process.ExitCode == 0)
                return (true, stdout);
            _logger.LogWarning("Command failed ({ExitCode}): {FileName} args=[{Args}] stderr={Stderr}",
                process.ExitCode, fileName, string.Join(", ", arguments), stderr);
            return (false, stderr);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to run: {FileName}", fileName);
            return (false, ex.Message);
        }
    }

    private async Task<(bool success, string output)> ExecuteCommand(string command, string args)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = command,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                }
            };

            process.Start();
            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
                return (true, output);
            else
                return (false, error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to execute command: {Command} {Args}", command, args);
            return (false, ex.Message);
        }
    }
}
