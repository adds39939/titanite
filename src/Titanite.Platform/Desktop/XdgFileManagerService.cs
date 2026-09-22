using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Desktop;
using System.Diagnostics;

namespace Titanite.Platform.Desktop;

public sealed class XdgFileManagerService(ILogger<XdgFileManagerService> logger) : IFileManagerService
{
    private const string OpenCommand = "xdg-open";

    private const string DetachCommand = "setsid";

    public DirectoryOpenStatus OpenDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            logger.LogInformation("There is no folder at {Path} to open.", path);

            return DirectoryOpenStatus.NotFound;
        }

        logger.LogInformation("Opening {Path} in the file manager.", path);

        return TryStart(BuildStartInfo(detached: true, path)) ||
               TryStart(BuildStartInfo(detached: false, path))
            ? DirectoryOpenStatus.Opened
            : DirectoryOpenStatus.Failed;
    }

    private bool TryStart(ProcessStartInfo startInfo)
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

    internal static ProcessStartInfo BuildStartInfo(bool detached, string path)
    {
        var startInfo = new ProcessStartInfo(detached ? DetachCommand : OpenCommand)
        {
            UseShellExecute = false
        };

        if (detached)
        {
            startInfo.ArgumentList.Add("--fork");
            startInfo.ArgumentList.Add(OpenCommand);
        }

        startInfo.ArgumentList.Add(path);

        return startInfo;
    }
}
