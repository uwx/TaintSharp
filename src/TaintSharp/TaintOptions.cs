using System;
using System.Buffers;
using System.Text;
using TaintSharp.Interop;

namespace TaintSharp;

/// <summary>Options for a <see cref="Taint.Translate(System.ReadOnlySpan{uint}, TaintOptions?)"/> call.</summary>
/// <remarks>
/// Instances are not thread-safe. Do not call any member on the same instance concurrently
/// with a translation that uses it.
/// </remarks>
public sealed unsafe class TaintOptions : IDisposable
{
    private NativeTaintOptions* _handle;

    /// <summary>Creates a default options object.</summary>
    /// <exception cref="OutOfMemoryException">The native library could not allocate the options object.</exception>
    public TaintOptions()
    {
        _handle = TaintNative.options_create();
        if (_handle == null)
        {
            throw new OutOfMemoryException("The native taint library failed to allocate a TaintOptions instance.");
        }
    }

    internal NativeTaintOptions* Handle
    {
        get
        {
            NativeTaintOptions* handle = _handle;
            if (handle == null)
            {
                throw new ObjectDisposedException(nameof(TaintOptions));
            }

            return handle;
        }
    }

    /// <summary>Sets a boolean option.</summary>
    /// <returns><see langword="true"/> if <paramref name="option"/> is a known option; otherwise <see langword="false"/>.</returns>
    public bool SetOption(TaintOption option, bool value)
        => TaintNative.options_set_bool(Handle, (Interop.TaintOption)option, value ? (byte)1 : (byte)0) != 0;

    /// <summary>Allows a WGSL extension by name, as written in a WGSL <c>enable</c> directive.</summary>
    /// <returns><see langword="true"/> if the extension name is recognized; otherwise <see langword="false"/>.</returns>
    public bool AllowWgslExtension(string extension) => SetWgslName(extension, isExtension: true);

    /// <summary>Allows a WGSL feature by name, as written in a WGSL <c>requires</c> directive.</summary>
    /// <returns><see langword="true"/> if the feature name is recognized; otherwise <see langword="false"/>.</returns>
    public bool AllowWgslFeature(string feature) => SetWgslName(feature, isExtension: false);

    /// <summary>Allows every WGSL extension that the bundled Tint knows about.</summary>
    public bool AllowAllWgslExtensions() => TaintNative.options_allow_all_wgsl_extensions(Handle) != 0;

    /// <summary>Allows every WGSL feature that the bundled Tint knows about.</summary>
    public bool AllowAllWgslFeatures() => TaintNative.options_allow_all_wgsl_features(Handle) != 0;

    /// <summary>
    /// Sets where taint should place the sampler for each combined image-sampler it splits.
    /// Unmapped samplers get the next free binding in their group.
    /// </summary>
    /// <returns><see langword="true"/> if the mappings were accepted; otherwise <see langword="false"/>.</returns>
    public bool SetSamplerMappings(ReadOnlySpan<TaintSamplerMapping> mappings)
    {
        NativeTaintOptions* handle = Handle;
        if (mappings.IsEmpty)
        {
            return TaintNative.options_set_sampler_mappings(handle, 0, null) != 0;
        }

        Interop.TaintSamplerMapping[] native = new Interop.TaintSamplerMapping[mappings.Length];
        for (int i = 0; i < mappings.Length; i++)
        {
            TaintSamplerMapping mapping = mappings[i];
            native[i] = new Interop.TaintSamplerMapping
            {
                from = new Interop.TaintBindingPoint { group = mapping.From.Group, binding = mapping.From.Binding },
                to = new Interop.TaintBindingPoint { group = mapping.To.Group, binding = mapping.To.Binding },
            };
        }

        fixed (Interop.TaintSamplerMapping* pointer = native)
        {
            return TaintNative.options_set_sampler_mappings(handle, (nuint)native.Length, pointer) != 0;
        }
    }

    /// <summary>Releases the native options object.</summary>
    public void Dispose()
    {
        NativeTaintOptions* handle = _handle;
        if (handle == null)
        {
            return;
        }

        _handle = null;
        TaintNative.options_destroy(handle);
        GC.SuppressFinalize(this);
    }

    /// <summary>Releases the native object if <see cref="Dispose"/> was not called.</summary>
    ~TaintOptions()
    {
        NativeTaintOptions* handle = _handle;
        if (handle != null)
        {
            _handle = null;
            TaintNative.options_destroy(handle);
        }
    }

    private bool SetWgslName(string name, bool isExtension)
    {
        NativeTaintOptions* handle = Handle;
        ArgumentNullException.ThrowIfNull(name);

        int byteCount = Encoding.UTF8.GetByteCount(name);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(byteCount + 1);
        try
        {
            int written = Encoding.UTF8.GetBytes(name, buffer);
            buffer[written] = 0;
            fixed (byte* pointer = buffer)
            {
                sbyte* text = (sbyte*)pointer;
                return isExtension
                    ? TaintNative.options_allow_wgsl_extension(handle, text) != 0
                    : TaintNative.options_allow_wgsl_feature(handle, text) != 0;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
