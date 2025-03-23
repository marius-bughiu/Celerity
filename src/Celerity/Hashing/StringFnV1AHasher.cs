using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

public struct StringFnV1AHasher : IHashProvider<string>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(string key)
    {
        // The FNV-1a 32-bit parameters
        const uint fnvPrime = 16777619;
        const uint offsetBasis = 2166136261;

        uint hash = offsetBasis;
        foreach (char c in key)
        {
            // Convert char to its lower byte; you may also want to consider 
            // encoding specifics if you deal with non-ASCII characters.
            hash ^= (byte)(c & 0xFF);
            hash *= fnvPrime;
        }

        // Cast back to int
        return unchecked((int)hash);
    }
}
