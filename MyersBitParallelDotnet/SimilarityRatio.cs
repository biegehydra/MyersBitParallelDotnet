namespace MyersBitParallel
{
    /// <summary>
    /// Result of a similarity comparison: the absolute Levenshtein
    /// <see cref="Distance"/> and a normalized <see cref="Ratio"/> in [0, 1].
    /// </summary>
    public readonly record struct SimilarityRatio(int Distance, double Ratio);
}
