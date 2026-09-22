using System.Buffers.Binary;
using System.Text;

namespace Titanite.Steam.Tests;

internal sealed record PublishedApp(uint AppId, string? Type, string? OperatingSystems);

internal static class FakeAppInfo
{
    private const uint MagicWithStringTable = 0x07564429;

    private const uint MagicInline = 0x07564428;

    private const int AppHeaderLength = 60;

    private static readonly string[] Keys = ["appinfo", "common", "name", "type", "oslist"];

    public static byte[] WithStringTable(params PublishedApp[] apps)
    {
        var indexOf = Keys
            .Select((key, index) => (key, index))
            .ToDictionary(pair => pair.key, pair => pair.index, StringComparer.Ordinal);

        var file = new MemoryStream();

        WriteUInt32(file, MagicWithStringTable);
        WriteUInt32(file, 1);

        var tableOffsetAt = (int)file.Position;

        WriteInt64(file, 0);
        WriteApps(file, apps, (stream, key) => WriteInt32(stream, indexOf[key]));

        var tableOffset = file.Position;

        WriteInt32(file, Keys.Length);

        foreach (var key in Keys)
        {
            WriteNullTerminated(file, key);
        }

        var bytes = file.ToArray();

        BinaryPrimitives.WriteInt64LittleEndian(bytes.AsSpan(tableOffsetAt), tableOffset);

        return bytes;
    }

    public static byte[] WithInlineKeys(params PublishedApp[] apps)
    {
        var file = new MemoryStream();

        WriteUInt32(file, MagicInline);
        WriteUInt32(file, 1);
        WriteApps(file, apps, WriteNullTerminated);

        return file.ToArray();
    }

    private static void WriteApps(Stream file, PublishedApp[] apps, Action<Stream, string> writeKey)
    {
        foreach (var app in apps)
        {
            var body = Body(app, writeKey);

            WriteUInt32(file, app.AppId);
            WriteUInt32(file, (uint)(body.Length + AppHeaderLength));
            file.Write(new byte[AppHeaderLength]);
            file.Write(body);
        }

        WriteUInt32(file, 0);
    }

    private static byte[] Body(PublishedApp app, Action<Stream, string> writeKey)
    {
        var body = new MemoryStream();

        body.WriteByte(0x00);
        writeKey(body, "appinfo");

        body.WriteByte(0x00);
        writeKey(body, "common");

        WriteString(body, writeKey, "name", $"App {app.AppId}");

        if (app.Type is { } type)
        {
            WriteString(body, writeKey, "type", type);
        }

        if (app.OperatingSystems is { } list)
        {
            WriteString(body, writeKey, "oslist", list);
        }

        body.WriteByte(0x08);
        body.WriteByte(0x08);

        return body.ToArray();
    }

    private static void WriteString(Stream body, Action<Stream, string> writeKey, string key, string value)
    {
        body.WriteByte(0x01);
        writeKey(body, key);
        WriteNullTerminated(body, value);
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        Span<byte> buffer = stackalloc byte[4];

        BinaryPrimitives.WriteUInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt32(Stream stream, int value)
    {
        Span<byte> buffer = stackalloc byte[4];

        BinaryPrimitives.WriteInt32LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteInt64(Stream stream, long value)
    {
        Span<byte> buffer = stackalloc byte[8];

        BinaryPrimitives.WriteInt64LittleEndian(buffer, value);
        stream.Write(buffer);
    }

    private static void WriteNullTerminated(Stream stream, string text)
    {
        stream.Write(Encoding.UTF8.GetBytes(text));
        stream.WriteByte(0);
    }
}
