using System.Diagnostics.CodeAnalysis;

#pragma warning disable IDE0130

namespace System.Runtime.CompilerServices;

[AttributeUsage(AttributeTargets.Method, Inherited = false)]
[ExcludeFromCodeCoverage]
internal sealed class ModuleInitializerAttribute : Attribute;

#pragma warning restore IDE0130