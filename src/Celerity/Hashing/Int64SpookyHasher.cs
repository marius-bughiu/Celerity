using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

public struct Int64SpookyHasher : IHashProvider<long>
{
    private const ulong SC_CONST = 0xDEADBEEFDEADBEEFul;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(long key)
    {
        ulong a = 0;
        ulong b = 0;
        ulong c = SC_CONST;
        ulong d = SC_CONST;

        c += (ulong)key;

        ShortEnd(ref a, ref b, ref c, ref d);

        return unchecked((int)c);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void ShortEnd(ref ulong a, ref ulong b, ref ulong c, ref ulong d)
    {
        d ^= c; c = Rot64(c, 15); d += c;
        a ^= d; d = Rot64(d, 52); a += d;
        b ^= a; a = Rot64(a, 26); b += a;
        c ^= b; b = Rot64(b, 51); c += b;
        d ^= c; c = Rot64(c, 28); d += c;
        a ^= d; d = Rot64(d, 9);  a += d;
        b ^= a; a = Rot64(a, 47); b += a;
        c ^= b; b = Rot64(b, 54); c += b;
        d ^= c; c = Rot64(c, 32); d += c;
        a ^= d; d = Rot64(d, 25); a += d;
        b ^= a; a = Rot64(a, 63); b += a;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Rot64(ulong x, int k) => (x << k) | (x >> (64 - k));
}
