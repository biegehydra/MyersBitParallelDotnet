namespace MyersBitParallel;

/// <summary>
/// Common contract implemented by every Myers bit-parallel distance engine
/// in this library. Engines are reusable, thread-affine instances that
/// produce <see cref="MyersPattern"/> handles for cheap repeated comparisons.
/// </summary>
public interface IMyersBitParallel
{
    /// <summary>
    /// Build a reusable pattern handle for <paramref name="pattern"/>. The
    /// caller owns the returned object and must dispose it when finished.
    /// </summary>
    MyersPattern Prepare(string pattern);

    /// <summary>
    /// Compute the Levenshtein distance between <paramref name="a"/> and
    /// <paramref name="b"/>. The pattern derived from <paramref name="a"/>
    /// is prepared and disposed automatically.
    /// </summary>
    int Distance(string a, string b);

    /// <summary>
    /// Compute the Levenshtein distance between an already-prepared
    /// <paramref name="pattern"/> and <paramref name="candidate"/>.
    /// </summary>
    int Distance(MyersPattern pattern, string candidate);

    /// <summary>
    /// Compute distance and a normalized similarity ratio between
    /// <paramref name="a"/> and <paramref name="b"/>.
    /// </summary>
    SimilarityRatio SimilarityRatio(string a, string b);

    /// <summary>
    /// Compute distance and a normalized similarity ratio between an
    /// already-prepared <paramref name="pattern"/> and
    /// <paramref name="candidate"/>.
    /// </summary>
    SimilarityRatio SimilarityRatio(MyersPattern pattern, string candidate);
}
