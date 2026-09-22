using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Processes;
using System.Diagnostics;
using System.Text;

namespace Titanite.Platform.Processes;

public sealed class FlatpakHostProcesses(ILogger<FlatpakHostProcesses> logger) : IHostProcesses
{
    private const string SpawnCommand = "flatpak-spawn";

    private const string ProcessQueryCommand = "pgrep";

    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(5);

    public bool IsRunning(string processName) =>
        Query(["-x", processName]);

    public bool AnyCommandLineContains(string fragment) =>
        Query(["-f", "--", ExtendedRegex.MatchingOthersOnly(fragment)]);

    public bool Start(string fileName, IReadOnlyList<string> arguments) =>
        DetachedProcess.TryStart(logger, OnHost(DetachedProcess.BuildStartInfo(detached: true, fileName, arguments))) ||
        DetachedProcess.TryStart(logger, OnHost(DetachedProcess.BuildStartInfo(detached: false, fileName, arguments)));

    private bool Query(IReadOnlyList<string> arguments)
    {
        var startInfo = OnHost(new ProcessStartInfo(ProcessQueryCommand, arguments));

        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        try
        {
            using var process = Process.Start(startInfo);

            if (process is null)
            {
                return false;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(QueryTimeout))
            {
                logger.LogWarning("{Command} on the host did not answer within {Timeout}.", ProcessQueryCommand, QueryTimeout);
                process.Kill();

                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not ask the host which processes are running.");

            return false;
        }
    }

    internal static ProcessStartInfo OnHost(ProcessStartInfo startInfo)
    {
        var hostStartInfo = new ProcessStartInfo(SpawnCommand)
        {
            UseShellExecute = false
        };

        hostStartInfo.ArgumentList.Add("--host");
        hostStartInfo.ArgumentList.Add(startInfo.FileName);

        foreach (var argument in startInfo.ArgumentList)
        {
            hostStartInfo.ArgumentList.Add(argument);
        }

        return hostStartInfo;
    }

    internal static class ExtendedRegex
    {
        private const string Special = @"\.[]()*+?{}|^$";

        public static string MatchingOthersOnly(string literal)
        {
            var escaped = new StringBuilder(literal.Length + 2);
            var bracketed = false;

            foreach (var character in literal)
            {
                if (!bracketed && char.IsAsciiLetterOrDigit(character))
                {
                    escaped.Append('[').Append(character).Append(']');
                    bracketed = true;
                }
                else
                {
                    if (Special.Contains(character))
                    {
                        escaped.Append('\\');
                    }

                    escaped.Append(character);
                }
            }

            return escaped.ToString();
        }
    }
}
