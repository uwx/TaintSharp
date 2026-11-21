using System;
using System.Runtime.InteropServices;
using TaintSharp.Interop;

namespace TaintSharp;

/// <summary>
/// The result of a translation. A module is returned even when translation fails; in that
/// case <see cref="GetWgsl"/> returns <see langword="null"/> and <see cref="GetDiagnostics"/>
/// describes the failure.
/// </summary>
public sealed unsafe class TaintModule : IDisposable
{
    private NativeTaintModule* _handle;

    internal TaintModule(NativeTaintModule* handle) => _handle = handle;

    private NativeTaintModule* Handle
    {
        get
        {
            NativeTaintModule* handle = _handle;
            if (handle == null)
            {
                throw new ObjectDisposedException(nameof(TaintModule));
            }

            return handle;
        }
    }

    /// <summary>Gets the translated WGSL source, or <see langword="null"/> if translation failed.</summary>
    /// <remarks>The returned string is a copy; it remains valid after this module is disposed.</remarks>
    public string? GetWgsl()
    {
        nuint size;
        sbyte* text = TaintNative.module_get_wgsl(Handle, &size);
        return text == null ? null : Marshal.PtrToStringUTF8((nint)text, checked((int)size));
    }

    /// <summary>
    /// Gets non-fatal warnings when translation succeeded, or error diagnostics when it failed.
    /// May be <see langword="null"/> even when translation failed.
    /// </summary>
    public string? GetDiagnostics()
    {
        nuint size;
        sbyte* text = TaintNative.module_get_diagnostics(Handle, &size);
        return text == null ? null : Marshal.PtrToStringUTF8((nint)text, checked((int)size));
    }

    /// <summary>Releases the native module and the strings it owns.</summary>
    public void Dispose()
    {
        NativeTaintModule* handle = _handle;
        if (handle == null)
        {
            return;
        }

        _handle = null;
        TaintNative.module_destroy(handle);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the native object if <see cref="Dispose"/> was not called.</summary>
    ~TaintModule()
    {
        NativeTaintModule* handle = _handle;
        if (handle != null)
        {
            _handle = null;
            TaintNative.module_destroy(handle);
        }
    }
}
