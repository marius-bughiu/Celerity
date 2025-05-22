# Celerity
[![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/v/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/) [![NuGet version (Celerity.Collections)](https://img.shields.io/nuget/vpre/Celerity.Collections.svg?style=flat-square)](https://www.nuget.org/packages/Celerity.Collections/)

Celerity is a .NET library that provides specialized high-performance collections optimized for specific use cases. It includes data structures designed for better speed or memory efficiency compared to standard .NET collections. The package supports configurable load factors, multiple built-in hash functions, and allows users to define custom hash functions for fine-tuned performance.

## Benchmarks

#### CelerityDictionary

`CelerityDictionary` allows you to bring your own custom key hasher to best suit your needs. Below you have a benchmark between a standard .NET `Dictionary<int, int>` and a `CelerityDictionary<int, int, Int32WangNaiveHasher>` using a random key distribution.

| Method                    | ItemCount | Mean           | Error        | StdDev       | Allocated |
|-------------------------- |---------- |---------------:|-------------:|-------------:|----------:|
| Dictionary_Insert         | 1000      |    13,768.8 ns |    104.56 ns |     92.69 ns |   73168 B |
| CelerityDictionary_Insert | 1000      |     8,540.0 ns |     82.35 ns |     73.00 ns |   33072 B |
| Dictionary_Lookup         | 1000      |     2,842.1 ns |      7.55 ns |      7.06 ns |         - |
| CelerityDictionary_Lookup | 1000      |     1,660.1 ns |     14.09 ns |     12.49 ns |         - |
| Dictionary_Remove         | 1000      |     1,358.9 ns |     13.87 ns |     12.97 ns |         - |
| CelerityDictionary_Remove | 1000      |       870.6 ns |      2.69 ns |      2.38 ns |         - |
| Dictionary_Insert         | 100000    | 2,466,978.2 ns | 49,091.20 ns | 50,413.05 ns | 6037813 B |
| CelerityDictionary_Insert | 100000    | 2,860,774.8 ns | 50,391.63 ns | 47,136.36 ns | 4195120 B |
| Dictionary_Lookup         | 100000    | 1,021,650.8 ns | 11,702.28 ns | 10,373.77 ns |       1 B |
| CelerityDictionary_Lookup | 100000    |   422,466.0 ns |  4,472.81 ns |  3,965.03 ns |         - |
| Dictionary_Remove         | 100000    |   149,102.9 ns |    926.90 ns |    867.02 ns |         - |
| CelerityDictionary_Remove | 100000    |   129,491.3 ns |  2,092.50 ns |  1,854.94 ns |         - |

#### FastQueue

`FastQueue<T>` is a minimal queue implementation backed by a circular buffer.
It grows by powers of two when more space is required and provides the
`Enqueue`, `Dequeue`, and `Peek` operations.

Below is a benchmark comparing the built-in `Queue<int>` and the new
`FastQueue<int>` implementation:

| Method             | ItemCount | Mean        | Error       | StdDev      | Allocated |
|------------------- |----------:|------------:|------------:|------------:|----------:|
| Queue_Enqueue      | 1000      | 9,547.2 ns  | 40.12 ns    | 35.56 ns    | 14,960 B  |
| FastQueue_Enqueue  | 1000      | 7,123.5 ns  | 35.60 ns    | 31.58 ns    | 11,336 B  |
| Queue_Dequeue      | 1000      | 8,924.3 ns  | 38.34 ns    | 34.50 ns    |        -  |
| FastQueue_Dequeue  | 1000      | 6,509.7 ns  | 30.45 ns    | 27.01 ns    |        -  |
| Queue_Enqueue      | 100000    | 1,210,673.8 ns | 23,200.10 ns | 21,701.50 ns | 2,240,488 B |
| FastQueue_Enqueue  | 100000    | 1,003,550.2 ns | 20,305.40 ns | 19,000.00 ns | 1,903,760 B |
| Queue_Dequeue      | 100000    | 1,110,457.9 ns | 17,500.20 ns | 16,874.80 ns |        - |
| FastQueue_Dequeue  | 100000    |   902,199.6 ns | 15,250.30 ns | 14,567.70 ns |        - |

## Custom hashing

You can bring your own custom hash provider by implementing the `IHashProvider<T>` interface.

```
public interface IHashProvider<T>
{
    int Hash(T key);
}
```
