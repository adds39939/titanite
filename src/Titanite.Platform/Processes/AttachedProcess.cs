using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Titanite.Platform.Processes;

internal static class AttachedProcess
{
    public static async Task<bool> RunAsync(
        ILogger logger,
        ProcessStartInfo startInfo,
        CancellationToken cancellationToken)
    {
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;

        Process? process;

        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not run {FileName}.", startInfo.FileName);

            return false;
        }

        if (process is null)
        {
            return false;
        }

        using (process)
        {
            var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
            var error = process.StandardError.ReadToEndAsync(CancellationToken.None);

            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);

                throw;
            }

            await Task.WhenAll(output, error).ConfigureAwait(false);

            if (process.ExitCode == 0)
            {
                return true;
            }

            logger.LogWarning(
                "{FileName} exited with {ExitCode}: {Error}",
                startInfo.FileName,
                process.ExitCode,
                (await error.ConfigureAwait(false)).Trim());

            return false;
        }
    }
}
