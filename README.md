# MyersBitParallel

[![NuGet](https://img.shields.io/nuget/v/MyersBitParallel.svg)](https://www.nuget.org/packages/MyersBitParallel/)

A high-throughput C# implementation of the **Myers bit-parallel Levenshtein
distance algorithm**, optimized for ASCII patterns up to 64 characters.

The single-word kernel evaluates one whole DP row per `ulong` operation, so
distance computation is `O(n)` machine instructions instead of `O(m·n)` cell
updates. For typical fuzzy-matching workloads (short query, many candidates,
small allowed edit distance) it runs **5×–20× faster** than a textbook
Wagner-Fischer DP, and **20×–200× faster** with the optional `maxDist` and
`requiredCharMask` prefilters engaged.

---

## Features

- **Single-word Myers bit-parallel kernel.** Distance for ASCII patterns up
  to 64 characters in one `ulong` of state.
- **Pattern reuse as a first-class API.** `Prepare` once, score many
  candidates without rebuilding the bit-mask table.
- **Threshold-aware fuzzy search.** Pass `maxDist` to short-circuit
  candidates that are guaranteed to be too far via length-difference,
  alphabet-overlap, and in-loop score cutoffs.
- **Required-symbol filter.** Pass `requiredCharMask` (built via
  `BuildCharMask`) to reject candidates that omit any pattern symbol before
  the kernel even runs.
- **Configurable character mapping at construction time.** Provide a
  `Func<char, byte>` that's invoked once per byte value to populate a
  256-entry lookup table — the hot loop never invokes the delegate again.
- **Built-in case-sensitive and case-insensitive engines** as
  `static readonly` instances; no per-call allocation.
- **Zero-allocation candidate paths** via `ArrayPool<T>` for prepared
  pattern buffers.
- **Multi-targets `netstandard2.1` and `net10.0`.**

---

## Installation

```shell
dotnet add package MyersBitParallel
```

Or in a `.csproj`:

```xml
<PackageReference Include="MyersBitParallel" Version="0.1.0" />
```

---

## Quick start

```csharp
using MyersBitParallel;

// Use one of the built-in engines.
var engine = MyersBitParallel64Ascii.CaseInsensitive;

int distance = engine.Distance("kitten", "sitting"); // 3

SimilarityRatio sim = engine.SimilarityRatio("hello", "helo");
// sim.Distance == 1, sim.Ratio == 0.8
```

The two ready-made engines are:

| Engine                                       | Mapper                          | Behavior                              |
| -------------------------------------------- | ------------------------------- | ------------------------------------- |
| `MyersBitParallel64Ascii.CaseSensitive`      | `AsciiMappers.CaseSensitive`    | Differences in case are significant   |
| `MyersBitParallel64Ascii.CaseInsensitive`    | `AsciiMappers.CaseInsensitive`  | Folds `A`–`Z` to `a`–`z`              |

---

## Reusing a pattern across many candidates

When you score one query against a large haystack, prepare the pattern
once and pass it by `in`:

```csharp
using MyersPattern64Ascii pat = engine.Prepare("kitten");
foreach (string candidate in haystack)
{
    int d = engine.Distance(in pat, candidate);
    // ...
}
// `using` returns the rented bit-mask buffer to ArrayPool.Shared.
```

Per-candidate cost is just the bit-parallel kernel — no allocation, no
mapper invocations, no rehashing.

---

## Threshold-aware fuzzy search

Pass `maxDist` to short-circuit any candidate whose distance is provably
greater than the threshold. The engine uses the length-difference, the
alphabet overlap, and an in-loop `score - remaining` cutoff to bail out as
early as possible.

```csharp
using MyersPattern64Ascii pat = engine.Prepare("apple");

foreach (string candidate in haystack)
{
    int d = engine.Distance(in pat, candidate, maxDist: 2);
    if (d != int.MaxValue)
    {
        // candidate is within 2 edits of "apple"
    }
}
```

For an even stricter prefilter, supply a `requiredCharMask` listing the
symbols every accepted candidate must contain. Build it from any reference
string with `BuildCharMask`:

```csharp
ulong required = engine.BuildCharMask("apple"); // pattern's char-mask

foreach (string candidate in haystack)
{
    int d = engine.Distance(in pat, candidate, maxDist: 2, requiredCharMask: required);
    if (d != int.MaxValue)
    {
        // candidate is within 2 edits AND contains every distinct symbol
        // that "apple" does (a, p, l, e).
    }
}
```

`requiredCharMask` is a 64-bit alphabet bitmap; mapped byte values are
folded to the low 6 bits, so it's a conservative filter (it never wrongly
rejects a valid match — at worst it lets a false positive through to the
kernel, which then produces the correct answer). It becomes even more helpful
if you generate your own engine with at most 64 distinct values.

---

## Custom character mapper

Pass any `Func<char, byte>` to the engine's constructor. It's called once
per byte value at construction time to build a 256-entry lookup table; the
hot loop reads the table directly with no further delegate dispatch.

```csharp
// Engine that treats every ASCII punctuation/whitespace character as
// equivalent (all collapsed to bucket 0). Letters are case-folded; digits
// are kept verbatim. Note: collapsing to a single bucket only erases the
// *identity* of those characters, not their position — both inputs still
// need to have the same length and the same shape.
var engine = new MyersBitParallel64Ascii(c =>
{
    if ((uint)(c - 'A') < 26u) return (byte)(c | 0x20); // fold A-Z to a-z
    if ((uint)(c - 'a') < 26u) return (byte)c;          // a-z verbatim
    if ((uint)(c - '0') < 10u) return (byte)c;          // 0-9 verbatim
    return 0;                                            // any non-alphanumeric
});

// Same length, same letter sequence; only the punctuation differs and
// every punctuation character maps to bucket 0, so distance is 0.
int same = engine.Distance("Hello, world!", "Hello& world?"); // 0

// Different lengths still cost edits — punctuation is collapsed, not deleted.
int diff = engine.Distance("Hello, world!", "hello world"); // 2
```

---

## API surface

| Type                        | Description                                                                 |
| --------------------------- | --------------------------------------------------------------------------- |
| `MyersBitParallel64Ascii`   | The engine: pattern prep, distance, similarity ratio, char-mask helper      |
| `MyersPattern64Ascii`       | Reusable prepared pattern, `IDisposable` to return its rented buffer        |
| `SimilarityRatio`           | `(int Distance, double Ratio)` record struct                                |
| `AsciiMappers.CaseSensitive` / `.CaseInsensitive` | Built-in `Func<char, byte>` mappers                  |

Key methods on `MyersBitParallel64Ascii`:

```csharp
int Distance(string a, string b, int maxDist = int.MaxValue, ulong requiredCharMask = 0);
int Distance(in MyersPattern64Ascii pattern, string candidate,
             int maxDist = int.MaxValue, ulong requiredCharMask = 0);

SimilarityRatio SimilarityRatio(string a, string b);
SimilarityRatio SimilarityRatio(in MyersPattern64Ascii pattern, string candidate);

MyersPattern64Ascii Prepare(string pattern);
ulong BuildCharMask(string s);
```

`Distance` returns `int.MaxValue` when the result is known to exceed
`maxDist`; otherwise the true edit distance.

---

## Limitations

- **ASCII only.** The engine takes a `Func<char, byte>` mapper. For
  non-ASCII data you'd have to pre-fold to a byte representation
  yourself, or wait for a future blocked-Myers Unicode kernel.
- **Pattern length capped at 64 characters** (the bit-vector is a single
  `ulong`). Throws `ArgumentException` on longer patterns.
- **Candidate length is unrestricted.**

---

## Target frameworks

- `netstandard2.1` — works on .NET Core 3.x, .NET 5+, Xamarin, Unity, Mono.
- `net10.0` — uses in-box `System.Numerics.BitOperations` and other
  modern intrinsics for the popcount paths.

---

## License

GNU Affero General Public License v3.0