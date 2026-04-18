using Celerity.Collections;
using Celerity.Hashing;

namespace Celerity.Tests.Hashing;

public class GuidHasherTests
{
    private readonly GuidHasher _hasher = new GuidHasher();

    // ── Exact-value anchors ───────────────────────────────────────────────────

    [Fact]
    public void Hash_Empty_ReturnsZero()
    {
        // Guid.Empty is all zero. Both 64-bit halves are 0, Murmur3 fmix64(0) is 0,
        // and 0 ^ 0 is 0. This also pins down the "reinterpret two halves, mix,
        // XOR, truncate" pipeline: any regression that, say, hashed a non-zero
        // seed in would break this test first.
        Assert.Equal(0, _hasher.Hash(Guid.Empty));
    }

    // ── Determinism ───────────────────────────────────────────────────────────

    [Fact]
    public void Hash_IsDeterministic_AcrossCalls()
    {
        Guid key = new Guid("12345678-1234-1234-1234-1234567890AB");
        int a = _hasher.Hash(key);
        int b = _hasher.Hash(key);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_IsDeterministic_AcrossInstances()
    {
        // Hashers are structs with no state, so two independently-constructed
        // instances must produce identical output for the same input.
        Guid key = new Guid("DEADBEEF-CAFE-BABE-F00D-123456789ABC");
        int a = new GuidHasher().Hash(key);
        int b = new GuidHasher().Hash(key);
        Assert.Equal(a, b);
    }

    // ── Avalanche ─────────────────────────────────────────────────────────────

    [Fact]
    public void Hash_LowHalfBits_InfluenceResult()
    {
        // Flip a single bit in the low half of the Guid; the hash must change.
        // Guards against a regression that only mixes the high half.
        var a = Guid.Empty;
        var b = new Guid(new byte[]
        {
            0x01, 0x00, 0x00, 0x00,   // first 4 bytes (_a)
            0x00, 0x00,               // _b
            0x00, 0x00,               // _c
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
        });

        Assert.NotEqual(_hasher.Hash(a), _hasher.Hash(b));
    }

    [Fact]
    public void Hash_HighHalfBits_InfluenceResult()
    {
        // Flip a single bit in the high half of the Guid; the hash must change.
        // Guards against a regression that only mixes the low half.
        var a = Guid.Empty;
        var b = new Guid(new byte[]
        {
            0x00, 0x00, 0x00, 0x00,
            0x00, 0x00,
            0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01,   // last byte
        });

        Assert.NotEqual(_hasher.Hash(a), _hasher.Hash(b));
    }

    [Fact]
    public void Hash_SharedPrefix_DivergesAcrossGuids()
    {
        // Database-generated Guids frequently share a long prefix and differ
        // only in the tail. A hasher that weights the prefix too heavily would
        // bunch these into a few buckets. This test catches that by asserting
        // two prefix-sharing Guids hash differently.
        var a = new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAA0");
        var b = new Guid("AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAA1");
        Assert.NotEqual(_hasher.Hash(a), _hasher.Hash(b));
    }

    [Fact]
    public void Hash_SharedSuffix_DivergesAcrossGuids()
    {
        // Mirror of the prefix test: two Guids that differ only in their leading
        // bytes should still produce distinct hashes.
        var a = new Guid("00000000-0000-0000-AAAA-AAAAAAAAAAAA");
        var b = new Guid("00000001-0000-0000-AAAA-AAAAAAAAAAAA");
        Assert.NotEqual(_hasher.Hash(a), _hasher.Hash(b));
    }

    // ── Distinctness sweep ────────────────────────────────────────────────────

    [Fact]
    public void Hash_DistinctInputs_ProduceDistinctResultsForSmallRange()
    {
        // 1000 sequential Guids (constructed from a monotonically increasing
        // low-half value) must produce 1000 distinct hashes. Each half passes
        // through Murmur3 fmix64 (a bijection on 64 bits) before truncation,
        // so a collision in this range would indicate the mixer is broken.
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            var bytes = new byte[16];
            // Write i into the first 4 bytes; leaves the remaining 12 bytes as zero.
            bytes[0] = (byte)(i & 0xFF);
            bytes[1] = (byte)((i >> 8) & 0xFF);
            bytes[2] = (byte)((i >> 16) & 0xFF);
            bytes[3] = (byte)((i >> 24) & 0xFF);

            var guid = new Guid(bytes);
            Assert.True(seen.Add(_hasher.Hash(guid)),
                $"Unexpected collision at iteration {i}.");
        }
    }

    [Fact]
    public void Hash_DistinctInputs_ProduceDistinctResultsForNewGuid()
    {
        // Second sweep driven by Guid.NewGuid(): exercises the high-entropy
        // end of the input space rather than the low-value sequential end.
        var seen = new HashSet<int>();
        for (int i = 0; i < 1000; i++)
        {
            Assert.True(seen.Add(_hasher.Hash(Guid.NewGuid())),
                $"Unexpected collision at iteration {i}.");
        }
    }

    // ── Does not throw ───────────────────────────────────────────────────────

    [Fact]
    public void Hash_DoesNotThrow()
    {
        Guid[] testValues =
        {
            Guid.Empty,
            Guid.NewGuid(),
            new Guid("FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF"),
            new Guid("80000000-0000-0000-0000-000000000000"),
            new Guid("00000000-0000-0000-0000-000000000001"),
        };

        foreach (Guid val in testValues)
        {
            var exception = Record.Exception(() => _hasher.Hash(val));
            Assert.Null(exception);
        }
    }

    // ── Integration: satisfies the hasher constraint on collections ──────────

    [Fact]
    public void GuidHasher_CanDriveCeleritySet()
    {
        var set = new CeleritySet<Guid, GuidHasher>();

        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var empty = Guid.Empty;   // default(Guid) — stored out-of-band

        set.Add(a);
        set.Add(b);
        set.Add(empty);

        Assert.Equal(3, set.Count);
        Assert.True(set.Contains(a));
        Assert.True(set.Contains(b));
        Assert.True(set.Contains(empty));
        Assert.False(set.Contains(Guid.NewGuid()));
    }

    [Fact]
    public void GuidHasher_CanDriveCelerityDictionary()
    {
        var dict = new CelerityDictionary<Guid, string, GuidHasher>();

        var key1 = Guid.NewGuid();
        var key2 = Guid.NewGuid();
        dict[key1] = "one";
        dict[key2] = "two";
        dict[Guid.Empty] = "zero";   // default(Guid) — out-of-band slot

        Assert.Equal(3, dict.Count);
        Assert.Equal("one", dict[key1]);
        Assert.Equal("two", dict[key2]);
        Assert.Equal("zero", dict[Guid.Empty]);
        Assert.True(dict.ContainsKey(key1));
        Assert.False(dict.ContainsKey(Guid.NewGuid()));
    }
}
