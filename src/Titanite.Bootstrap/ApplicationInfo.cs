using Titanite.Abstractions.Hosting;
using System.Reflection;

namespace Titanite.Bootstrap;

internal sealed class ApplicationInfo(IAppEnvironment environment) : IApplicationInfo
{
    public string Version { get; } = ReadVersion(typeof(ApplicationInfo).Assembly);

    public Uri RepositoryUrl { get; } = new("https://github.com/adds39939/titanite");

    public bool IsDevelopment => environment.IsDevelopment;

    internal static string ReadVersion(Assembly assembly)
    {
        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrWhiteSpace(informational))
        {
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }

        var metadata = informational.IndexOf('+');

        return metadata < 0 ? informational : informational[..metadata];
    }
}
