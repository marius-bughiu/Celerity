# Utilities API Reference

## FastUtils

```csharp
public static class FastUtils
```

Lives in the `Celerity` namespace. Provides low-level helper methods used internally by the collection implementations. Public so that advanced users can reuse them.

### NextPowerOfTwo

```csharp
public static int NextPowerOfTwo(int n)
```

Returns the smallest power of two that is greater than or equal to `n`.

**Special cases:**

- `n <= 0` returns `1`.
- `n >= 2^30` returns `2^30` (1,073,741,824) to prevent integer overflow.
- If `n` is already a power of two, it is returned unchanged.

**Used by** `CelerityDictionary` and `IntDictionary` to round the user-supplied capacity to a power of two, which enables fast index computation via bitwise AND instead of modulo.
