namespace MyersBitParallel;

/// <summary>
/// Result of a similarity comparison: the Levenshtein
/// <see cref="Distance"/> (or a sentinel) and a normalized <see cref="Ratio"/> in <c>[0, 1]</c>.
/// </summary>
/// <remarks>
/// When <see cref="Distance"/> is <see cref="int.MaxValue"/>, it is the same sentinel as
/// <see cref="MyersBitParallel64.Distance(System.String,System.String,System.Int32,System.UInt64)"/>
/// (no finite distance: over <c>maxDist</c>, failed optional filters, etc.). In that case
/// <see cref="Ratio"/> is <c>0</c>: not a numeric edit distance, but the minimum similarity in
/// <c>[0, 1]</c>.
/// </remarks>
/// <param name="Distance">Finite Levenshtein distance, or <see cref="int.MaxValue"/> as a sentinel.</param>
/// <param name="Ratio"><c>1 - Distance / max(|a|, |b|)</c> for finite distances (or <c>1</c> when both lengths are zero); <c>0</c> when <paramref name="Distance"/> is <see cref="int.MaxValue"/>.</param>
public readonly record struct SimilarityRatio(int Distance, double Ratio);
