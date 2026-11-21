using System;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using TaintSharp.Interop;

namespace TaintSharp;

/// <summary>Translates SPIR-V modules to WGSL using taint (a packaged build of Google's Tint).</summary>
public static unsafe class Taint
{
    /// <summary>The version reported by the loaded native library.</summary>
    public static Version LibraryVersion { get; } = DecodeVersion(TaintNative.library_version());

    /// <summary>Translates a SPIR-V module stored as 32-bit words.</summary>
    /// <param name="spirv">The SPIR-V module, starting with its 5-word header.</param>
    /// <param name="options">Options for this translation, or <see langword="null"/> for defaults.</param>
    /// <returns>A module object. Inspect <see cref="TaintModule.GetWgsl"/> and <see cref="TaintModule.GetDiagnostics"/> for the result.</returns>
    /// <exception cref="OutOfMemoryException">The native library could not allocate the result.</exception>
    /// <exception cref="ArgumentException"><paramref name="spirv"/> is shorter than the 5-word SPIR-V header.</exception>
    /// <remarks>
    /// The native library is documented as potentially crashing the process on malformed input.
    /// Only pass SPIR-V produced by a trusted compiler.
    /// </remarks>
    public static TaintModule Translate(ReadOnlySpan<uint> spirv, TaintOptions? options = null)
    {
        if (spirv.Length < 5)
        {
            throw new ArgumentException("SPIR-V must contain at least the 5-word module header.", nameof(spirv));
        }

        fixed (uint* words = spirv)
        {
            NativeTaintOptions* nativeOptions = options is null ? null : options.Handle;
            NativeTaintModule* module = TaintNative.spirv_to_wgsl((nuint)spirv.Length, words, nativeOptions);
            if (module == null)
            {
                throw new OutOfMemoryException("The native taint library failed to allocate a TaintModule.");
            }

            return new TaintModule(module);
        }
    }

    /// <summary>Translates a SPIR-V module stored as little-endian bytes.</summary>
    /// <param name="spirv">The SPIR-V module bytes. The length must be a multiple of 4.</param>
    /// <param name="options">Options for this translation, or <see langword="null"/> for defaults.</param>
    /// <exception cref="ArgumentException"><paramref name="spirv"/> is not a whole number of 32-bit words.</exception>
    public static TaintModule Translate(ReadOnlySpan<byte> spirv, TaintOptions? options = null)
    {
        if ((spirv.Length & 3) != 0)
        {
            throw new ArgumentException("SPIR-V byte length must be a multiple of 4.", nameof(spirv));
        }

        ReadOnlySpan<uint> words = MemoryMarshal.Cast<byte, uint>(spirv);
        if (!BitConverter.IsLittleEndian)
        {
            uint[] swapped = new uint[words.Length];
            for (int i = 0; i < swapped.Length; i++)
            {
                swapped[i] = BinaryPrimitives.ReverseEndianness(words[i]);
            }

            words = swapped;
        }

        return Translate(words, options);
    }

    private static Version DecodeVersion(uint version) => new(
        (int)(version / 1_000_000),
        (int)(version / 1_000 % 1_000),
        (int)(version % 1_000));
}
