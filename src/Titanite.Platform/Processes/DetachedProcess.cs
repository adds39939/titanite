using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Titanite.Platform.Processes;

internal static class DetachedProcess
{
    private const string DetachCommand = "setsid";

    public static bool TryStart(ILogger logger, string fileName, IReadOnlyList<string> arguments) =>
        TryStart(logger, BuildStartInfo(detached: true, fileName, arguments)) ||
        TryStart(logger, BuildStartInfo(detached: false, fileName, arguments));

    public static bool TryStart(ILogger logger, ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);

            return process is not null;
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not run {FileName}.", startInfo.FileName);

            return false;
        }
    }

    public static ProcessStartInfo BuildStartInfo(bool detached, string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo(detached ? DetachCommand : fileName)
        {
            UseShellExecute = false
        };

        if (detached)
        {
            startInfo.ArgumentList.Add("--fork");
            startInfo.ArgumentList.Add(fileName);
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
