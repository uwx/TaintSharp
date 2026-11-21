namespace TaintSharp;

/// <summary>Options that control how taint translates SPIR-V to WGSL.</summary>
public enum TaintOption
{
    /// <summary>Allow derivative builtins in non-uniform control flow.</summary>
    AllowNonUniformDerivatives = 0,

    /// <summary>Allow subgroup operations in non-uniform control flow.</summary>
    AllowNonUniformSubgroupOperations = 1,

    /// <summary>Do not emit the unreachable-code warning.</summary>
    DisableUnreachableCodeWarning = 2,

    /// <summary>Emit WGSL without unnecessary whitespace.</summary>
    Minify = 3,
}

/// <summary>A WGSL binding point, identified by bind group and binding index.</summary>
public readonly struct TaintBindingPoint
{
    /// <summary>Creates a binding point.</summary>
    public TaintBindingPoint(uint group, uint binding)
    {
        Group = group;
        Binding = binding;
    }

    /// <summary>The bind group index (SPIR-V <c>DescriptorSet</c>).</summary>
    public uint Group { get; }

    /// <summary>The binding index within the bind group.</summary>
    public uint Binding { get; }
}

/// <summary>
/// Maps a combined image-sampler binding point to the binding point that should be
/// used for the sampler that taint splits out of it.
/// </summary>
public readonly struct TaintSamplerMapping
{
    /// <summary>Creates a sampler mapping.</summary>
    public TaintSamplerMapping(TaintBindingPoint from, TaintBindingPoint to)
    {
        From = from;
        To = to;
    }

    /// <summary>The binding point of the combined image-sampler in the SPIR-V module.</summary>
    public TaintBindingPoint From { get; }

    /// <summary>The binding point the split-out sampler should use.</summary>
    public TaintBindingPoint To { get; }
}
