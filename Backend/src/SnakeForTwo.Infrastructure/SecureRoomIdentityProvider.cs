using System.Security.Cryptography;
using SnakeForTwo.Game.Application;

namespace SnakeForTwo.Infrastructure;

public sealed class SecureRoomIdentityProvider : IRoomIdentityProvider
{
    private const string RoomAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public string CreateRoomId() => CreateCode(length: 12);

    public string CreatePlayerId() => $"player_{CreateToken(bytes: 12)}";

    public string CreatePlayerSessionToken() => CreateToken(bytes: 32);

    public string CreateMatchId() => $"match_{CreateToken(bytes: 12)}";

    public int CreateSeed() => RandomNumberGenerator.GetInt32(int.MinValue, int.MaxValue);

    private static string CreateCode(int length)
    {
        Span<char> chars = stackalloc char[length];
        for (var index = 0; index < chars.Length; index++)
        {
            chars[index] = RoomAlphabet[RandomNumberGenerator.GetInt32(RoomAlphabet.Length)];
        }

        return new string(chars);
    }

    private static string CreateToken(int bytes)
    {
        Span<byte> buffer = stackalloc byte[bytes];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToHexString(buffer).ToLowerInvariant();
    }
}
