using System;
using System.Buffers.Binary;
using System.IO;
using TaintSharp;
using Xunit;

namespace TaintSharp.Tests;

public sealed class TaintTests
{
    private static string SpirvPath => Path.Combine(AppContext.BaseDirectory, "assets", "basic.frag.spv");

    [Fact]
    public void TranslatesFragmentShader()
    {
        using TaintModule module = Taint.Translate(LoadWords());
        string wgsl = Assert.IsType<string>(module.GetWgsl());

        Assert.Contains("@fragment", wgsl, StringComparison.Ordinal);
        Assert.Contains("fn main", wgsl, StringComparison.Ordinal);
    }

    [Fact]
    public void TranslatesByteInput()
    {
        using TaintModule module = Taint.Translate(File.ReadAllBytes(SpirvPath));
        Assert.IsType<string>(module.GetWgsl());
    }

    [Fact]
    public void RejectsMisalignedByteInput()
    {
        Assert.Throws<ArgumentException>(() => Taint.Translate(new byte[3]));
    }

    [Fact]
    public void RejectsShortInput()
    {
        Assert.Throws<ArgumentException>(() => Taint.Translate(Array.Empty<uint>()));
        Assert.Throws<ArgumentException>(() => Taint.Translate(new byte[4]));
    }

    [Fact]
    public void MinifyReducesOutputSize()
    {
        using TaintOptions options = new();
        Assert.True(options.SetOption(TaintOption.Minify, true));

        using TaintModule normalModule = Taint.Translate(LoadWords());
        using TaintModule minifiedModule = Taint.Translate(LoadWords(), options);
        string normal = Assert.IsType<string>(normalModule.GetWgsl());
        string minified = Assert.IsType<string>(minifiedModule.GetWgsl());

        Assert.True(minified.Length < normal.Length, $"Minified output was not shorter ({minified.Length} vs {normal.Length}).");
    }

    [Fact]
    public void SamplerMappingMovesSamplerBinding()
    {
        using TaintOptions options = new();
        Assert.True(options.SetSamplerMappings(new[]
        {
            new TaintSamplerMapping(new TaintBindingPoint(0, 0), new TaintBindingPoint(0, 7)),
        }));

        using TaintModule module = Taint.Translate(LoadWords(), options);
        string wgsl = Assert.IsType<string>(module.GetWgsl());

        Assert.Contains("@binding(7u) var tex_sampler", wgsl, StringComparison.Ordinal);
        Assert.Contains(": sampler", wgsl, StringComparison.Ordinal);
    }

    [Fact]
    public void InvalidSpirvReturnsDiagnostics()
    {
        uint[] words = LoadWords();
        using TaintModule module = Taint.Translate(words.AsSpan(0, words.Length - 1));

        Assert.Null(module.GetWgsl());
        Assert.False(string.IsNullOrEmpty(module.GetDiagnostics()));
    }

    [Fact]
    public void UnknownOptionIsRejected()
    {
        using TaintOptions options = new();
        Assert.False(options.SetOption((TaintOption)999, true));
    }

    [Fact]
    public void UnknownExtensionIsRejected()
    {
        using TaintOptions options = new();
        Assert.False(options.AllowWgslExtension("definitely_not_a_wgsl_extension"));
    }

    [Fact]
    public void EmptySamplerMappingsAreAccepted()
    {
        using TaintOptions options = new();
        Assert.True(options.SetSamplerMappings(ReadOnlySpan<TaintSamplerMapping>.Empty));
    }

    [Fact]
    public void OptionsDisposeIsIdempotentAndUseAfterDisposeThrows()
    {
        TaintOptions options = new();
        options.Dispose();
        options.Dispose();

        Assert.Throws<ObjectDisposedException>(() => options.SetOption(TaintOption.Minify, true));
    }

    [Fact]
    public void ModuleDisposeIsIdempotentAndUseAfterDisposeThrows()
    {
        TaintModule module = Taint.Translate(LoadWords());
        module.Dispose();
        module.Dispose();

        Assert.Throws<ObjectDisposedException>(() => module.GetWgsl());
    }

    private static uint[] LoadWords()
    {
        byte[] bytes = File.ReadAllBytes(SpirvPath);
        Assert.Equal(0, bytes.Length % 4);

        uint[] words = new uint[bytes.Length / 4];
        for (int i = 0; i < words.Length; i++)
        {
            words[i] = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(i * 4, 4));
        }

        return words;
    }
}
