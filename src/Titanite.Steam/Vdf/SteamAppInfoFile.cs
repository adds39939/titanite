using System.Buffers.Binary;
using System.Text;

namespace Titanite.Steam.Vdf;

internal static class SteamAppInfoFile
{
    private const uint MagicWithStringTable = 0x07564429;

    private const uint MagicInline = 0x07564428;

    private const uint MagicLegacy = 0x07564427;

    private const int AppHeaderLength = 60;

    private const byte NestedObject = 0x00;

    private const byte StringValue = 0x01;

    private const byte Int32Value = 0x02;

    private const byte FloatValue = 0x03;

    private const byte PointerValue = 0x04;

    private const byte WideStringValue = 0x05;

    private const byte ColourValue = 0x06;

    private const byte UInt64Value = 0x07;

    private const byte EndOfObject = 0x08;

    private const byte Int64Value = 0x0A;

    public static IReadOnlyDictionary<uint, SteamAppMetadata> Read(
        string path,
        IReadOnlySet<uint>? appIds = null)
    {
        try
        {
            return Parse(File.ReadAllBytes(path), appIds);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OutOfMemoryException)
        {
            return new Dictionary<uint, SteamAppMetadata>();
        }
    }

    internal static IReadOnlyDictionary<uint, SteamAppMetadata> Parse(
        ReadOnlySpan<byte> file,
        IReadOnlySet<uint>? appIds = null)
    {
        var found = new Dictionary<uint, SteamAppMetadata>();

        if (file.Length < 8)
        {
            return found;
        }

        var magic = BinaryPrimitives.ReadUInt32LittleEndian(file);

        if (magic is not (MagicWithStringTable or MagicInline or MagicLegacy))
        {
            return found;
        }

        var position = 8;
        var keys = magic == MagicWithStringTable ? ReadStringTable(file, ref position) : null;

        while (position + 8 <= file.Length)
        {
            var appId = BinaryPrimitives.ReadUInt32LittleEndian(file[position..]);

            if (appId == 0)
            {
                break;
            }

            var length = BinaryPrimitives.ReadUInt32LittleEndian(file[(position + 4)..]);

            if (length < AppHeaderLength || length > file.Length - position - 8)
            {
                break;
            }

            var next = position + 8 + (int)length;

            if ((appIds is null || appIds.Contains(appId)) &&
                ReadCommon(file[(position + 8 + AppHeaderLength)..next], keys) is { } metadata)
            {
                found[appId] = metadata;
            }

            position = next;
        }

        return found;
    }

    private static string[] ReadStringTable(ReadOnlySpan<byte> file, ref int position)
    {
        var offset = BinaryPrimitives.ReadInt64LittleEndian(file[position..]);

        position += 8;

        if (offset < 0 || offset + 4 > file.Length)
        {
            return [];
        }

        var at = (int)offset;
        var count = BinaryPrimitives.ReadInt32LittleEndian(file[at..]);

        if (count < 0)
        {
            return [];
        }

        at += 4;

        var strings = new string[count];

        for (var index = 0; index < count; index++)
        {
            strings[index] = ReadNullTerminated(file, ref at);
        }

        return strings;
    }

    private static SteamAppMetadata? ReadCommon(ReadOnlySpan<byte> body, string[]? keys)
    {
        var position = 0;

        return Enter(body, ref position, keys, "appinfo") && Enter(body, ref position, keys, "common")
            ? ReadTypeAndOperatingSystems(body, ref position, keys)
            : null;
    }

    private static bool Enter(ReadOnlySpan<byte> body, ref int position, string[]? keys, string wanted)
    {
        while (position < body.Length)
        {
            var kind = body[position++];

            if (kind == EndOfObject)
            {
                return false;
            }

            var key = ReadKey(body, ref position, keys);

            if (kind == NestedObject && string.Equals(key, wanted, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            Skip(kind, body, ref position, keys);
        }

        return false;
    }

    private static SteamAppMetadata? ReadTypeAndOperatingSystems(
        ReadOnlySpan<byte> body,
        ref int position,
        string[]? keys)
    {
        string? type = null;
        string? operatingSystems = null;

        while (position < body.Length)
        {
            var kind = body[position++];

            if (kind == EndOfObject)
            {
                break;
            }

            var key = ReadKey(body, ref position, keys);

            if (kind == StringValue && string.Equals(key, "type", StringComparison.OrdinalIgnoreCase))
            {
                type = ReadNullTerminated(body, ref position);
            }
            else if (kind == StringValue && string.Equals(key, "oslist", StringComparison.OrdinalIgnoreCase))
            {
                operatingSystems = ReadNullTerminated(body, ref position);
            }
            else
            {
                Skip(kind, body, ref position, keys);
            }
        }

        return type is null && operatingSystems is null
            ? null
            : new SteamAppMetadata(type, operatingSystems);
    }

    private static void Skip(byte kind, ReadOnlySpan<byte> body, ref int position, string[]? keys)
    {
        switch (kind)
        {
            case NestedObject:
                SkipObject(body, ref position, keys);

                break;
            case StringValue:
                SkipNullTerminated(body, ref position);

                break;
            case WideStringValue:
                SkipWideString(body, ref position);

                break;
            case Int32Value or FloatValue or PointerValue or ColourValue:
                position += 4;

                break;
            case UInt64Value or Int64Value:
                position += 8;

                break;
            default:
                position = body.Length;

                break;
        }
    }

    private static void SkipObject(ReadOnlySpan<byte> body, ref int position, string[]? keys)
    {
        while (position < body.Length)
        {
            var kind = body[position++];

            if (kind == EndOfObject)
            {
                return;
            }

            SkipKey(body, ref position, keys);
            Skip(kind, body, ref position, keys);
        }
    }

    private static void SkipKey(ReadOnlySpan<byte> body, ref int position, string[]? keys)
    {
        if (keys is null)
        {
            SkipNullTerminated(body, ref position);
        }
        else
        {
            position = Math.Min(position + 4, body.Length);
        }
    }

    private static string ReadKey(ReadOnlySpan<byte> body, ref int position, string[]? keys)
    {
        if (keys is null)
        {
            return ReadNullTerminated(body, ref position);
        }

        if (position + 4 > body.Length)
        {
            position = body.Length;

            return string.Empty;
        }

        var index = BinaryPrimitives.ReadInt32LittleEndian(body[position..]);

        position += 4;

        return index >= 0 && index < keys.Length ? keys[index] : string.Empty;
    }

    private static string ReadNullTerminated(ReadOnlySpan<byte> body, ref int position)
    {
        if (position >= body.Length)
        {
            return string.Empty;
        }

        var end = body[position..].IndexOf((byte)0);

        if (end < 0)
        {
            var rest = Encoding.UTF8.GetString(body[position..]);

            position = body.Length;

            return rest;
        }

        var text = Encoding.UTF8.GetString(body.Slice(position, end));

        position += end + 1;

        return text;
    }

    private static void SkipNullTerminated(ReadOnlySpan<byte> body, ref int position)
    {
        var end = position < body.Length ? body[position..].IndexOf((byte)0) : -1;

        position = end < 0 ? body.Length : position + end + 1;
    }

    private static void SkipWideString(ReadOnlySpan<byte> body, ref int position)
    {
        while (position + 2 <= body.Length)
        {
            var unit = BinaryPrimitives.ReadUInt16LittleEndian(body[position..]);

            position += 2;

            if (unit == 0)
            {
                return;
            }
        }

        position = body.Length;
    }
}
