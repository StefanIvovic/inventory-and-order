using System.Buffers.Text;
using System.Text.Json;

namespace BuildingBlocks.Lookups;

internal sealed record LookupCursor(string Name, Guid Id)
{
    public string Encode() => Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(this));

    public static bool TryDecode(string? value, out LookupCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return true;
        }

        try
        {
            cursor = JsonSerializer.Deserialize<LookupCursor>(Base64Url.DecodeFromChars(value));
            return cursor is { Name: not null } && cursor.Id != Guid.Empty;
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            cursor = null;
            return false;
        }
    }
}
