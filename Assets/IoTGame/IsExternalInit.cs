// Polyfill required for C# 9 record types in Unity.
// Unity's .NET runtime does not include this type — declaring it here enables records project-wide.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
