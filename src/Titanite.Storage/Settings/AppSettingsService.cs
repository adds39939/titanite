using Microsoft.Extensions.Logging;
using Titanite.Abstractions.Settings;
using Titanite.Core.Settings;

using System.Text.Json.Serialization;
using System.Text.Json;

namespace Titanite.Storage.Settings;

public sealed class AppSettingsService(
    ITitaniteStorage storage,
    ILogger<AppSettingsService> logger) : IAppSettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly SemaphoreSlim _lock = new(1, 1);

    private AppSettings? _settings;

    public AppSettings Get()
    {
        if (_settings is not null)
        {
            return _settings;
        }

        _lock.Wait();

        try
        {
            if (_settings is not null)
            {
                return _settings;
            }

            return _settings = File.Exists(storage.SettingsFile)
                ? Parse(File.ReadAllText(storage.SettingsFile))
                : new AppSettings();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return _settings = Defaults(e);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<AppSettings> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_settings is not null)
        {
            return _settings;
        }

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_settings is not null)
            {
                return _settings;
            }

            if (!File.Exists(storage.SettingsFile))
            {
                return _settings = new AppSettings();
            }

            var json = await File.ReadAllTextAsync(storage.SettingsFile, cancellationToken).ConfigureAwait(false);

            return _settings = Parse(json);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            return _settings = Defaults(e);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var sanitised = settings.Sanitised();

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            Directory.CreateDirectory(storage.Root);

            await File.WriteAllTextAsync(
                storage.SettingsFile,
                JsonSerializer.Serialize(sanitised, SerializerOptions),
                cancellationToken).ConfigureAwait(false);

            _settings = sanitised;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            logger.LogError(e, "Could not write settings to {SettingsPath}.", storage.SettingsFile);

            throw;
        }
        finally
        {
            _lock.Release();
        }
    }

    private static AppSettings Parse(string json) =>
        (JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions) ?? new AppSettings()).Sanitised();

    private AppSettings Defaults(Exception exception)
    {
        logger.LogWarning(
            exception,
            "Could not read settings at {SettingsPath}; using the defaults.",
            storage.SettingsFile);

        return new AppSettings();
    }
}
