# MyersBitParallel

[![NuGet](https://img.shields.io/nuget/v/MyersBitParallel.svg)](https://www.nuget.org/packages/MyersBitParallel/)

A high-throughput C# implementation of the **Myers bit-parallel Levenshtein
distance algorithm**, optimized for ASCII patterns up to 64 characters.
Ships two engines:

- **`MyersBitParallel64`** — full-string edit distance between two equal-ish
  length inputs.
- **`MyersSubstringBitParallel64`** — *best-match* distance: the minimum
  edit distance between a short pattern and any contiguous substring of a
  longer haystack (a.k.a. semi-global / approximate substring search).

The single-word kernel evaluates one whole DP row per `ulong` operation, so
distance computation is `O(n)` machine instructions instead of `O(m·n)` cell
updates. For typical fuzzy-matching workloads (short query, many candidates,
small allowed edit distance) it runs **5×–20× faster** than a textbook
Wagner-Fischer DP, and **20×–200× faster** with the optional `maxDist` and
`requiredCharMask` prefilters engaged.

**Further reading:** [Optimizing Levenshtein for Fuzzy Name Matching](https://connorhallman.com/blog/optimizing-levenshtein) — design notes, animations, and pruning layers.

---

## Features

- **Single-word Myers bit-parallel kernel.** Distance for ASCII patterns up
  to 64 characters in one `ulong` of state.
- **Full-string and best-match substring modes.** `MyersBitParallel64` for
  end-to-end Levenshtein; `MyersSubstringBitParallel64` for "find this
  fuzzy term inside this longer text" via a single-pass semi-global kernel.
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
<PackageReference Include="MyersBitParallel" Version="0.2.3" />
```

---

## Quick start

```csharp
using MyersBitParallel;

// Use one of the built-in engines.
var engine = MyersBitParallel64.AsciiCaseInsensitive;

int distance = engine.Distance("kitten", "sitting"); // 3

SimilarityRatio sim = engine.SimilarityRatio("hello", "helo");
// sim.Distance == 1, sim.Ratio == 0.8
```

The two ready-made engines are:

| Engine                                       | Mapper                          | Behavior                              |
| -------------------------------------------- | ------------------------------- | ------------------------------------- |
| `MyersBitParallel64.AsciiCaseSensitive`      | `AsciiMappers.CaseSensitive`    | Differences in case are significant   |
| `MyersBitParallel64.AsciiCaseInsensitive`    | `AsciiMappers.CaseInsensitive`  | Folds `A`–`Z` to `a`–`z`              |

The engine itself is alphabet-agnostic — it operates on whatever
`byte` bucket your `Func<char, byte>` mapper returns. The two statics
above are convenience instances wired with the built-in `AsciiMappers`;
build your own engine with `new MyersBitParallel64(myMapper)` for any
other mapping you like.

---

## Reusing a pattern across many candidates

When you score one query against a large haystack, prepare the pattern
once and pass it by `in`:

```csharp
using MyersPattern64 pat = engine.Prepare("kitten");
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
using MyersPattern64 pat = engine.Prepare("apple");

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

## Best-match substring search

`MyersSubstringBitParallel64` answers a different question than
`MyersBitParallel64`: given a short pattern and a longer haystack, what's
the *minimum edit distance to any contiguous substring of the haystack?*
This is the "fuzzy find-in-string" operation — classically solved by
semi-global DP filling an `(m+1) × (n+1)` matrix, here done in a single
`O(n)` bit-parallel pass over the haystack.

```csharp
using MyersBitParallel;

var engine = MyersSubstringBitParallel64.CaseInsensitive;

int d = engine.BestMatchDistance("BOAT", "THE LONG MOAT OF THE CASTLE");
// d == 1   (best window = "MOAT", one substitution)

int same = engine.BestMatchDistance("HELLO", "SAY HELLO WORLD");
// same == 0   (exact substring match)

int fuzzy = engine.BestMatchDistance("JSMITH", "USER_ID=JSMTH42");
// fuzzy == 1   (one deletion: "JSMTH")
```

Reuse a prepared pattern across many haystacks exactly like the full-string
engine:

```csharp
using MyersSubstringPattern64 pat = engine.Prepare("jsmith");
foreach (string row in logLines)
{
    if (engine.BestMatchDistance(in pat, row) <= 2)
    {
        // row contains something within 2 edits of "jsmith"
    }
}
```

Semantics in edge cases:

- `BestMatchDistance("", text)` is `0` — the empty substring always matches.
- `BestMatchDistance(pattern, "")` is `pattern.Length`.
- When `pattern` is longer than `text`, the result is the minimum Levenshtein
  distance between `pattern` and any substring of `text` (including the full
  text), bounded below by `pattern.Length - text.Length`. Useful for ranking
  short candidates against a longer query.

The engine exposes the same constructors, `Prepare`, `CaseSensitive` /
`CaseInsensitive` statics, and 64-char pattern limit as `MyersBitParallel64`.
Key methods:

```csharp
int BestMatchDistance(string pattern, string text);
int BestMatchDistance(in MyersSubstringPattern64 pattern, string text);
MyersSubstringPattern64 Prepare(string query);
```

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
var engine = new MyersBitParallel64(c =>
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

| Type                              | Description                                                                 |
| --------------------------------- | --------------------------------------------------------------------------- |
| `MyersBitParallel64`              | Full-string engine: distance, similarity ratio, char-mask helper            |
| `MyersPattern64`                  | Reusable prepared pattern for the full-string engine                        |
| `MyersSubstringBitParallel64`     | Best-match substring engine                                                 |
| `MyersSubstringPattern64`         | Reusable prepared pattern for the substring engine                          |
| `SimilarityRatio`                 | `(int Distance, double Ratio)` record struct                                |
| `AsciiMappers.CaseSensitive` / `.CaseInsensitive` | Built-in `Func<char, byte>` mappers                        |

Key methods on `MyersBitParallel64`:

```csharp
int Distance(string a, string b, int maxDist = int.MaxValue, ulong requiredCharMask = 0);
int Distance(in MyersPattern64 pattern, string candidate,
             int maxDist = int.MaxValue, ulong requiredCharMask = 0);

SimilarityRatio SimilarityRatio(string a, string b, int maxDist = int.MaxValue, ulong requiredCharMask = 0);
SimilarityRatio SimilarityRatio(in MyersPattern64 pattern, string candidate, int maxDist = int.MaxValue, ulong requiredCharMask = 0);

MyersPattern64 Prepare(string pattern);
ulong BuildCharMask(string s);
```

`Distance` returns `int.MaxValue` when the result is known to exceed
`maxDist`; otherwise the true edit distance.

---

## Benchmarks

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) with the
`ShortRun` job; **Ratio** is each method's mean time divided by the fastest
row (lower is better). Machine, runtime, and job settings affect absolute
numbers; see the [blog post](https://connorhallman.com/blog/optimizing-levenshtein)
for full tables and methodology.

### Full-string distance (`OneToManyMaxDist64Benchmark`)

One prepared query, 1000 noisy ASCII candidates, case-insensitive distance,
`MyersBitParallel64.AsciiCaseInsensitive`.

| Method | MaxDist | CandidateCount | Mean | Ratio |
|--------|--------:|---------------:|-----:|------:|
| `Myers_PreparedOnce_WithMaxDist` | 3 | 1000 | 5.415 μs | 1.00 |
| `Myers_PreparedOnce_NoMaxDist` | 3 | 1000 | 21.652 μs | 4.00 |
| `NaiveLevenshteinReference_NoMaxDist` | 3 | 1000 | 202.914 μs | 37.48 |
| `NaiveLevenshteinReference_WithMaxDist` | 3 | 1000 | 63.057 μs | 11.65 |
| `WagnerFischerReference_WithMaxDist` | 3 | 1000 | 42.496 μs | 7.85 |
| `UkkonenReference_WithMaxDist` | 3 | 1000 | 51.068 μs | 9.43 |

### Best-match substring search (`OneToManyBestMatch64Benchmark`)

Eight distinct ASCII queries each scored against `HaystackCount` haystacks
(~10-word filler sentences with one noisy copy of the query embedded at a
random offset). Case-insensitive; every reference is a fair apples-to-apples
implementation of the same *min-over-substrings* quantity.

| Method | HaystackCount | Mean | Ratio | Allocated |
|--------|---------------:|-----:|------:|----------:|
| `Myers_PreparedOnce`    |  100 |    0.65 ms |  1.00 |        0 B |
| `Myers_PerCallPrepare`  |  100 |    0.72 ms |  1.10 |        0 B |
| `SemiGlobal_TwoRow`     |  100 |    5.16 ms |  7.90 |   1,133 kB |
| `SemiGlobal_FullMatrix` |  100 |    6.73 ms | 10.29 |   5,091 kB |
| `Myers_PreparedOnce`    | 1000 |    6.91 ms |  1.00 |        0 B |
| `Myers_PerCallPrepare`  | 1000 |    7.12 ms |  1.03 |        0 B |
| `SemiGlobal_TwoRow`     | 1000 |   39.57 ms |  5.73 |  11,315 kB |
| `SemiGlobal_FullMatrix` | 1000 |   65.42 ms |  9.47 |  50,834 kB |

Measured on an Intel Core i5-12600K, .NET 10.0.5. The bit-parallel kernel is
~6–10× faster than an equivalent-semantics semi-global Wagner-Fischer and
allocates zero bytes per call vs. tens of megabytes for the DP references.

---

## Limitations

- **Single-byte alphabet.** The engine maps each `char` to a single
  `byte` via your `Func<char, byte>` mapper. Anything that fits into 256
  buckets works (ASCII, Latin-1, a custom Unicode-fold table, etc.); for
  full Unicode you'd have to pre-fold to a byte representation yourself,
  or wait for a future blocked-Myers Unicode kernel.
- **Pattern length capped at 64 characters** (the bit-vector is a single
  `ulong`). Both engines throw `ArgumentException` on longer patterns.
- **Candidate / haystack length is unrestricted.**

---

## Target frameworks

- `netstandard2.1` — works on .NET Core 3.x, .NET 5+, Xamarin, Unity, Mono.
- `net10.0` — uses in-box `System.Numerics.BitOperations` and other
  modern intrinsics for the popcount paths.

---

## License

GNU Affero General Public License v3.0
