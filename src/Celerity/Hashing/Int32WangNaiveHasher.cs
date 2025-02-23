using System.Runtime.CompilerServices;

namespace Celerity.Hashing;

public struct Int32WangNaiveHasher : IHashProvider<int>
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Hash(int key) => key ^ (key >> 16);

}
