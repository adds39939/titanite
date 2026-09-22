namespace Titanite.Abstractions.Hosting;

public interface IApplicationInfo
{
    string Version { get; }

    Uri RepositoryUrl { get; }
}
