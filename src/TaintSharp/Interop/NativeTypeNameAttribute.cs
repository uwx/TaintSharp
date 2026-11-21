using System;

namespace TaintSharp.Interop;

// ClangSharp emits [NativeTypeName] in its output but only materializes the attribute
// type when generating helper types. The generated interop file is entirely internal,
// so a local definition is sufficient and keeps that output unmodified.
[AttributeUsage(AttributeTargets.All, AllowMultiple = false)]
internal sealed class NativeTypeNameAttribute : Attribute
{
    public NativeTypeNameAttribute(string name) => Name = name;

    public string Name { get; }
}
