#!/usr/bin/env bash
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

python native/build.py --fetch-only

dotnet tool restore

dotnet tool run ClangSharpPInvokeGenerator -- \
  -x c \
  -f native/taint/include/taint.h \
  -I native/taint/include \
  -I native/clang-include \
  -n TaintSharp.Interop \
  -m TaintNative \
  -l taint \
  -p taint_ \
  -c codegen=default \
  -c file=single \
  -c types=unix \
  -hf native/bindings-header.txt \
  -rt TaintOptions=NativeTaintOptions \
  -rt TaintModule=NativeTaintModule \
  -r TAINT_OPT_WGSL_ALLOW_NON_UNIFORM_DERIVATIVES=AllowNonUniformDerivatives \
  -r TAINT_OPT_WGSL_ALLOW_NON_UNIFORM_SUBGROUP_OPERATIONS=AllowNonUniformSubgroupOperations \
  -r TAINT_OPT_WGSL_DISABLE_UNREACHABLE_CODE_WARNING=DisableUnreachableCodeWarning \
  -r TAINT_OPT_WGSL_MINIFY=Minify \
  -was '*=internal' \
  -o src/TaintSharp/Interop/taint.g.cs
