using System.Collections.Concurrent;

namespace Titanite.Steam;

internal sealed class FileStampCache<T>
{
    private readonly ConcurrentDictionary<string, (FileStamp Stamp, T Value)> _entries = new(StringComparer.Ordinal);

    public async Task<T> GetAsync(
        string path,
        Func<string, CancellationToken, Task<T>> read,
        CancellationToken cancellationToken = default)
    {
        var stamp = FileStamp.Of(path);

        if (_entries.TryGetValue(path, out var held) && held.Stamp == stamp)
        {
            return held.Value;
        }

        var value = await read(path, cancellationToken).ConfigureAwait(false);

        _entries[path] = (stamp, value);

        return value;
    }

    private readonly record struct FileStamp(bool Exists, DateTime Modified, long Length)
    {
        public static FileStamp Of(string path)
        {
            try
            {
                var file = new FileInfo(path);

                return file.Exists
                    ? new FileStamp(true, file.LastWriteTimeUtc, file.Length)
                    : default;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return default;
            }
        }
    }
}
