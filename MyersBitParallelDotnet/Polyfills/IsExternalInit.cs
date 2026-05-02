#if !NET5_0_OR_GREATER
using System.ComponentModel;

namespace System.Runtime.CompilerServices;
// Required by the C# compiler to emit `init` accessors and `record` types
// when targeting frameworks that do not ship this type.
[EditorBrowsable(EditorBrowsableState.Never)]
internal static class IsExternalInit
{
}
#endif
