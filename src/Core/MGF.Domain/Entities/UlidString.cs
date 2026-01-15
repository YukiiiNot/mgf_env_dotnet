using System.Security.Cryptography;

namespace MGF.Domain.Entities;

public static class UlidString
{
    private const string Base32CrockfordLower = "0123456789abcdefghjkmnpqrstvwxyz";

    public static string New()
    {
        Span<byte> bytes = stackalloc byte[16];

        var timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(timestampMs >> 40);
        bytes[1] = (byte)(timestampMs >> 32);
        bytes[2] = (byte)(timestampMs >> 24);
        bytes[3] = (byte)(timestampMs >> 16);
        bytes[4] = (byte)(timestampMs >> 8);
        bytes[5] = (byte)timestampMs;

        RandomNumberGenerator.Fill(bytes[6..]);

        return Encode(bytes);
    }

    private static string Encode(ReadOnlySpan<byte> bytes)
    {
        Span<char> chars = stackalloc char[26];

        chars[0] = Base32CrockfordLower[(bytes[0] & 0xE0) >> 5];
        chars[1] = Base32CrockfordLower[bytes[0] & 0x1F];
        chars[2] = Base32CrockfordLower[(bytes[1] & 0xF8) >> 3];
        chars[3] = Base32CrockfordLower[((bytes[1] & 0x07) << 2) | ((bytes[2] & 0xC0) >> 6)];
        chars[4] = Base32CrockfordLower[(bytes[2] & 0x3E) >> 1];
        chars[5] = Base32CrockfordLower[((bytes[2] & 0x01) << 4) | ((bytes[3] & 0xF0) >> 4)];
        chars[6] = Base32CrockfordLower[((bytes[3] & 0x0F) << 1) | ((bytes[4] & 0x80) >> 7)];
        chars[7] = Base32CrockfordLower[(bytes[4] & 0x7C) >> 2];
        chars[8] = Base32CrockfordLower[((bytes[4] & 0x03) << 3) | ((bytes[5] & 0xE0) >> 5)];
        chars[9] = Base32CrockfordLower[bytes[5] & 0x1F];
        chars[10] = Base32CrockfordLower[(bytes[6] & 0xF8) >> 3];
        chars[11] = Base32CrockfordLower[((bytes[6] & 0x07) << 2) | ((bytes[7] & 0xC0) >> 6)];
        chars[12] = Base32CrockfordLower[(bytes[7] & 0x3E) >> 1];
        chars[13] = Base32CrockfordLower[((bytes[7] & 0x01) << 4) | ((bytes[8] & 0xF0) >> 4)];
        chars[14] = Base32CrockfordLower[((bytes[8] & 0x0F) << 1) | ((bytes[9] & 0x80) >> 7)];
        chars[15] = Base32CrockfordLower[(bytes[9] & 0x7C) >> 2];
        chars[16] = Base32CrockfordLower[((bytes[9] & 0x03) << 3) | ((bytes[10] & 0xE0) >> 5)];
        chars[17] = Base32CrockfordLower[bytes[10] & 0x1F];
        chars[18] = Base32CrockfordLower[(bytes[11] & 0xF8) >> 3];
        chars[19] = Base32CrockfordLower[((bytes[11] & 0x07) << 2) | ((bytes[12] & 0xC0) >> 6)];
        chars[20] = Base32CrockfordLower[(bytes[12] & 0x3E) >> 1];
        chars[21] = Base32CrockfordLower[((bytes[12] & 0x01) << 4) | ((bytes[13] & 0xF0) >> 4)];
        chars[22] = Base32CrockfordLower[((bytes[13] & 0x0F) << 1) | ((bytes[14] & 0x80) >> 7)];
        chars[23] = Base32CrockfordLower[(bytes[14] & 0x7C) >> 2];
        chars[24] = Base32CrockfordLower[((bytes[14] & 0x03) << 3) | ((bytes[15] & 0xE0) >> 5)];
        chars[25] = Base32CrockfordLower[bytes[15] & 0x1F];

        return new string(chars);
    }
}
