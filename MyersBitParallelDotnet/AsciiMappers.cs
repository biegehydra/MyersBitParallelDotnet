namespace MyersBitParallel;

/// <summary>
/// Built-in <see cref="char"/>-to-<see cref="byte"/> mappers suitable for
/// the ASCII engines. Each delegate is invoked once per code point in the
/// constructor of an engine to populate a 256-entry lookup table; the
/// hot loop of the engine never invokes the delegate again.
/// </summary>
public static class AsciiMappers
{
    /// <summary>
    /// Identity mapping that preserves the low 8 bits of the input
    /// character verbatim. Differences in case, punctuation, and digits
    /// are all significant.
    /// </summary>
    public static byte CaseSensitive(char c) => unchecked((byte)c);

    /// <summary>
    /// Folds ASCII A-Z to a-z and otherwise preserves the low 8 bits of
    /// the input character.
    /// </summary>
    public static byte CaseInsensitive(char c)
    {
        if ((uint)(c - 'A') < 26u)
            return unchecked((byte)(c | 0x20));
        return unchecked((byte)c);
    }
}
