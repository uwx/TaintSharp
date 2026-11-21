# TaintSharp

.NET bindings for [taint](https://github.com/taisei-project/taint), a small C library that
translates SPIR-V to WGSL. taint is a stripped-down, Meson-built packaging of Google's Tint.

Native libraries for `win-x64`, `linux-x64`, and `osx-arm64` ship inside the NuGet package under
`runtimes/<rid>/native/`. The managed assembly targets `net8.0` and `net10.0`.

## Install

```bash
dotnet add package TaintSharp
```

## Usage

```csharp
using TaintSharp;

byte[] spirv = File.ReadAllBytes("shader.spv");

using TaintOptions options = new();
options.SetOption(TaintOption.Minify, true);

// WGSL has no combined image-samplers. Tell taint where to put the sampler it splits out.
options.SetSamplerMappings(new[]
{
    new TaintSamplerMapping(new TaintBindingPoint(group: 0, binding: 0), new TaintBindingPoint(group: 0, binding: 1)),
});

using TaintModule module = Taint.Translate(spirv, options);

string? wgsl = module.GetWgsl();
if (wgsl is null)
{
    throw new InvalidOperationException(module.GetDiagnostics());
}
```

- `Taint.Translate` accepts `ReadOnlySpan<uint>` or `ReadOnlySpan<byte>`; bytes are read as
  little-endian 32-bit words and the length must be a multiple of 4.
- A module is returned even when translation fails. `GetWgsl` then returns `null` and
  `GetDiagnostics` describes the error.
- `TaintModule` and `TaintOptions` are finalizable, but call `Dispose` to release native memory
  promptly. Using either after disposal throws `ObjectDisposedException`.
- `Taint.LibraryVersion` reports the version of the loaded native library.

### Caveats

- taint's own documentation warns that Tint can crash the process on malformed SPIR-V. Only
  translate modules produced by a trusted compiler.
- Only the three RIDs above are packaged. musl, `linux-arm64`, `win-arm64`, and `osx-x64` are not.
- The Linux build targets glibc 2.34+ (it is produced on Ubuntu 22.04).
- `TaintOptions` must not be used by more than one translation at a time.

## Building from source

Requirements: Git, Python 3.9+, .NET SDK 10+, a C++20 toolchain (MSVC 2022+ on Windows),
Meson, and Ninja.

```bash
git clone https://github.com/uwx/TaintSharp
cd TaintSharp
python -m pip install meson ninja
python native/build.py --rid win-x64   # or linux-x64 / osx-arm64; default is the host RID
dotnet test tests/TaintSharp.Tests/TaintSharp.Tests.csproj -c Release
```

`native/build.py` checks out the pinned taint revision into `native/taint` (the revision is recorded
in the script) and lets Meson download the `SPIRV-Tools` and `SPIRV-Headers` wrap subprojects.
On Windows it activates the Visual Studio environment and builds with MSVC; the built library is
staged in `artifacts/native/<rid>/`.

## Regenerating the P/Invoke bindings

`src/TaintSharp/Interop/taint.g.cs` is generated from `native/taint/include/taint.h` by
`ClangSharpPInvokeGenerator`, pinned in `.config/dotnet-tools.json`:

```bash
bash native/generate-bindings.sh
```

CI regenerates the file and fails if it differs from the committed version.

## Releasing

CI builds and tests the native library on all three RIDs, then packs one package containing all of
them. Pushing a `v*` tag publishes that package to nuget.org with
[NuGet trusted publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)
(OIDC), so no long-lived API key is stored in the repository.

One-time setup:

1. On nuget.org, add a trusted publishing policy for the repository, workflow file `ci.yml`, and
   (optionally) environment `release`.
2. In GitHub, create an environment named `release` and a repository secret `NUGET_USER`
   containing your nuget.org profile name.
3. Push a tag, for example `git tag v0.1.0 && git push origin v0.1.0`.

## License

The managed wrapper is MIT-licensed. The native library embeds taint (BSD-3-Clause AND Zlib),
Tint, SPIRV-Tools (Apache-2.0), and SPIRV-Headers. See `LICENSE` and `THIRD-PARTY-NOTICES.md`.
