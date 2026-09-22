namespace Titanite.Abstractions.Hosting;

public interface ICustomSchemeHandler
{
    string Scheme { get; }

    SchemeContent? Open(string? url);
}

public sealed record SchemeContent(Stream Content, string ContentType);
