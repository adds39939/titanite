using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Processes;
using System.Diagnostics;

namespace Titanite.Platform.Processes;

public sealed class NativeHostProcesses(ILogger<NativeHostProcesses> logger) : IHostProcesses
{
    public bool IsRunning(string processName)
    {
        try
        {
            var processes = Process.GetProcessesByName(processName);

            foreach (var process in processes)
            {
                process.Dispose();
            }

            return processes.Length > 0;
        }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException)
        {
            logger.LogWarning(e, "Could not determine whether {ProcessName} is running.", processName);

            return false;
        }
    }

    public bool AnyCommandLineContains(string fragment)
    {
        try
        {
            foreach (var directory in Directory.EnumerateDirectories("/proc"))
            {
                if (!int.TryParse(Path.GetFileName(directory), out _))
                {
                    continue;
                }

                string commandLine;

                try
                {
                    commandLine = File.ReadAllText(Path.Combine(directory, "cmdline"));
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    continue;
                }

                if (commandLine.Replace('\0', ' ').Contains(fragment, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(e, "Could not scan the running processes.");
        }

        return false;
    }

    public bool Start(string fileName, IReadOnlyList<string> arguments) =>
        DetachedProcess.TryStart(logger, fileName, arguments);

    public Task<bool> RunAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken = default) =>
        AttachedProcess.RunAsync(logger, new ProcessStartInfo(fileName, arguments), cancellationToken);
}
