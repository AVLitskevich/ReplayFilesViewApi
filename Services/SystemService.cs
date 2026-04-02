using System.Diagnostics;
using System.Runtime.InteropServices;
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
        else // DockerCompose
        {
            var composePath = string.IsNullOrEmpty(project.RestartPath) ? "docker-compose.yml" : project.RestartPath;
            command = "docker";
            args = $"compose -f {composePath} restart {project.RestartServiceName}";
        }

        return await ExecuteCommand(command, args);
    }

    public async IAsyncEnumerable<string> StreamLogs(ProjectSettings project, [EnumeratorCancellation] CancellationToken ct)
    {
        string command;
        string args;

        if (project.RestartType == RestartType.Systemd)
        {
            command = "sudo";
            args = $"journalctl -xefu {project.RestartServiceName}";
        }
        else // Docker
        {
            if (project.LogType == LogType.File && !string.IsNullOrEmpty(project.LogPath))
            {
                command = "docker";
                args = $"exec {project.RestartServiceName} tail -f {project.LogPath}";
            }
            else
            {
                var composePath = string.IsNullOrEmpty(project.RestartPath) ? "docker-compose.yml" : project.RestartPath;
                command = "docker";
                args = $"compose -f {composePath} logs -f {project.RestartServiceName}";
            }
        }

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

        if (process.Start())
        {
            _logger.LogInformation("Started log stream for {Slug} with command: {Command} {Args}", project.Slug, command, args);
            
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
