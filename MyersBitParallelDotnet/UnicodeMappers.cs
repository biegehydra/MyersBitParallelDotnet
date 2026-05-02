#if NET5_0_OR_GREATER
using System.Text;
#endif

namespace MyersBitParallel
{
    /// <summary>
    /// Built-in mappers suitable for the Unicode engines. The exposed
    /// signatures intentionally differ between target frameworks: modern
    /// runtimes use <see cref="System.Text.Rune"/>, while
    /// <c>netstandard2.1</c> falls back to raw <see cref="int"/> Unicode
    /// scalar values.
    /// </summary>
    public static class UnicodeMappers
    {
#if NET5_0_OR_GREATER
        /// <summary>
        /// Identity mapping. Every input rune is returned unchanged.
        /// </summary>
        public static Rune Identity(Rune r) => r;

        /// <summary>
        /// Invariant lowercase fold using <see cref="Rune.ToLowerInvariant(Rune)"/>.
        /// Suitable for ASCII-heavy text and most BMP scripts; for advanced
        /// Unicode normalization, supply a custom delegate.
        /// </summary>
        public static Rune SimpleInvariantCaseFold(Rune r) => Rune.ToLowerInvariant(r);
#else
        /// <summary>
        /// Identity mapping. Every input code point is returned unchanged.
        /// </summary>
        public static int Identity(int codePoint) => codePoint;

        /// <summary>
        /// Best-effort invariant lowercase fold. ASCII A-Z and BMP code
        /// points are folded via <see cref="char.ToLowerInvariant(char)"/>;
        /// supplementary plane code points are returned unchanged. Supply a
        /// custom delegate if you need full Unicode case folding outside the
        /// BMP.
        /// </summary>
        public static int SimpleInvariantCaseFold(int codePoint)
        {
            if ((uint)(codePoint - 'A') < 26u)
                return codePoint | 0x20;

            if ((uint)codePoint <= 0xFFFFu && !char.IsSurrogate((char)codePoint))
                return char.ToLowerInvariant((char)codePoint);

            return codePoint;
        }
#endif
    }
}
