using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Desktop;
using Titanite.Platform.Processes;
using System.Diagnostics;

namespace Titanite.Platform.Desktop;

public sealed class XdgFileManagerService(ILogger<XdgFileManagerService> logger) : IFileManagerService
{
    private const string OpenCommand = "xdg-open";

    public DirectoryOpenStatus OpenDirectory(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            logger.LogInformation("There is no folder at {Path} to open.", path);

            return DirectoryOpenStatus.NotFound;
        }

        logger.LogInformation("Opening {Path} in the file manager.", path);

        return DetachedProcess.TryStart(logger, BuildStartInfo(detached: true, path)) ||
               DetachedProcess.TryStart(logger, BuildStartInfo(detached: false, path))
            ? DirectoryOpenStatus.Opened
            : DirectoryOpenStatus.Failed;
    }

    internal static ProcessStartInfo BuildStartInfo(bool detached, string path) =>
        DetachedProcess.BuildStartInfo(detached, OpenCommand, [path]);
}
