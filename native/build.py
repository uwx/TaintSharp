#!/usr/bin/env python3
"""Build the native taint shared library and stage it for NuGet packaging.

The resulting file is written to artifacts/native/<rid>/<library name>.
The pinned taint sources are fetched into native/taint on demand. Requires Meson, Ninja, and a
C++20 compiler.
"""

from __future__ import annotations

import argparse
import os
import platform
import shutil
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
TAINT_SRC = ROOT / "native" / "taint"
BUILD_ROOT = ROOT / "native" / "build"
OUTPUT_ROOT = ROOT / "artifacts" / "native"

# taint has no releases and Meson needs the sources, so pin the upstream commit here.
TAINT_URL = "https://github.com/taisei-project/taint.git"
TAINT_REVISION = "1c27145adfeebce72c40bea3ce21a7977e14ad59"

HOST_RIDS = {
    ("Windows", "AMD64"): "win-x64",
    ("Linux", "x86_64"): "linux-x64",
    ("Darwin", "arm64"): "osx-arm64",
    ("Darwin", "x86_64"): "osx-x64",
}

LIBRARY_NAMES = {
    "win-x64": "taint.dll",
    "linux-x64": "libtaint.so",
    "osx-arm64": "libtaint.dylib",
    "osx-x64": "libtaint.dylib",
}


def fail(message: str) -> "NoReturn":
    raise SystemExit(f"error: {message}")


def meson_command() -> list[str]:
    executable = shutil.which("meson")
    if executable:
        return [executable]
    return [sys.executable, "-m", "mesonbuild.mesonmain"]


def add_python_scripts_to_path(env: dict[str, str]) -> None:
    # 'pip install meson ninja' installs console scripts next to the interpreter
    # (Scripts/ on Windows). Meson shells out to ninja by name, so make sure that
    # directory is on PATH even when the SDK did not add it.
    scripts = Path(sys.executable).resolve().parent / "Scripts"
    if not scripts.is_dir():
        scripts = Path(sys.executable).resolve().parent
    parts = env.get("PATH", "").split(os.pathsep)
    if str(scripts) not in parts:
        env["PATH"] = os.pathsep.join([str(scripts), *parts])


def ensure_taint_sources() -> None:
    marker = TAINT_SRC / "include" / "taint.h"
    if marker.exists():
        try:
            head = subprocess.run(
                ["git", "-C", str(TAINT_SRC), "rev-parse", "HEAD"],
                check=True,
                capture_output=True,
                text=True,
            ).stdout.strip()
        except (OSError, subprocess.CalledProcessError):
            head = ""
        if head == TAINT_REVISION:
            return
        # Definitely do not delete a user's own checkout with local changes.
        status = subprocess.run(
            ["git", "-C", str(TAINT_SRC), "status", "--porcelain"],
            capture_output=True,
            text=True,
        ).stdout.strip()
        if status:
            fail(f"{TAINT_SRC} is dirty; commit, clean, or remove it before building")

    shutil.rmtree(TAINT_SRC, ignore_errors=True)
    TAINT_SRC.parent.mkdir(parents=True, exist_ok=True)
    print(f"fetching taint {TAINT_REVISION}")
    for command in (
        ["git", "init", "--quiet", str(TAINT_SRC)],
        ["git", "-C", str(TAINT_SRC), "remote", "add", "origin", TAINT_URL],
        ["git", "-C", str(TAINT_SRC), "fetch", "--depth", "1", "origin", TAINT_REVISION],
        ["git", "-C", str(TAINT_SRC), "checkout", "--quiet", "--detach", "FETCH_HEAD"],
    ):
        subprocess.run(command, check=True)


def patch_spirv_tools_for_msvc() -> None:
    # SPIRV-Tools' Meson project forces cpp_std=gnu++17 through override_options.
    # Meson rejects gnu++17 for MSVC, so pin the standard dialect instead. The
    # library only needs C++17.
    path = TAINT_SRC / "subprojects" / "SPIRV-Tools" / "meson.build"
    if not path.exists():
        fail(f"{path} is missing; run 'meson subprojects download --sourcedir native/taint'")
    text = path.read_text(encoding="utf-8")
    if "cpp_std=gnu++17" not in text:
        if "cpp_std=c++17" in text:
            return
        fail("SPIRV-Tools meson.build changed; update native/build.py")
    path.write_text(text.replace("cpp_std=gnu++17", "cpp_std=c++17"), encoding="utf-8", newline="\n")


def write_native_file() -> Path:
    # SPIRV-Tools calls import('python').find_installation('python3'). On Windows
    # no such executable normally exists, and the Microsoft Store alias is not a
    # usable interpreter. Point Meson at the interpreter running this script.
    path = BUILD_ROOT / "native.ini"
    path.parent.mkdir(parents=True, exist_ok=True)
    python = sys.executable.replace("\\", "/")
    path.write_text(f"[binaries]\npython = '{python}'\n", encoding="utf-8")
    return path


def write_linux_native_file() -> Path:
    # A Meson array option cannot carry these through -D (the comma is taken
    # literally), so set them in a native file instead.
    path = BUILD_ROOT / "linux.ini"
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(
        "[built-in options]\n"
        "cpp_link_args = ['-static-libstdc++', '-static-libgcc']\n",
        encoding="utf-8",
    )
    return path


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--rid", choices=sorted(LIBRARY_NAMES), help="NuGet runtime identifier (default: host)")
    parser.add_argument("--clean", action="store_true", help="delete the Meson build directory first")
    parser.add_argument("--fetch-only", action="store_true", help="only check out the pinned taint sources")
    args = parser.parse_args()

    ensure_taint_sources()
    if args.fetch_only:
        return 0

    rid = args.rid or HOST_RIDS.get((platform.system(), platform.machine()))
    if rid is None:
        fail(f"unsupported host {platform.system()} {platform.machine()}; pass --rid explicitly")
    library_name = LIBRARY_NAMES[rid]
    build_dir = BUILD_ROOT / rid

    if args.clean:
        shutil.rmtree(build_dir, ignore_errors=True)

    meson = meson_command()
    env = os.environ.copy()
    add_python_scripts_to_path(env)

    subprocess.run(
        meson + ["subprojects", "download", "--sourcedir", str(TAINT_SRC)],
        check=True,
        env=env,
    )

    setup = meson + ["setup", str(build_dir), str(TAINT_SRC), "--buildtype=release", "--prefix=/"]
    if os.name == "nt":
        patch_spirv_tools_for_msvc()
        setup += [
            "--vsenv",
            "--native-file",
            str(write_native_file()),
            "-Dcpp_args=/Zc:preprocessor",
            # Static CRT so the DLL does not require the Visual C++ redistributable.
            "-Db_vscrt=mt",
        ]
    elif sys.platform.startswith("linux"):
        # Keep the shared library free of a libstdc++/libgcc version requirement.
        setup += ["--native-file", str(write_linux_native_file())]
    if (build_dir / "build.ninja").exists():
        setup.append("--reconfigure")
    elif (build_dir / "meson-private").exists():
        # A previous setup failed before writing build.ninja; start over.
        shutil.rmtree(build_dir, ignore_errors=True)

    subprocess.run(setup, check=True, env=env)
    subprocess.run(
        meson + ["compile", "-C", str(build_dir), "-j", str(os.cpu_count() or 4), "taint"],
        check=True,
        env=env,
    )

    if rid == "win-x64":
        candidates = sorted(build_dir.glob("taint-*.dll")) + [build_dir / "taint.dll"]
    elif rid.startswith("osx-"):
        candidates = [build_dir / "libtaint.dylib"] + sorted(build_dir.glob("libtaint.*.dylib"))
    else:
        candidates = [build_dir / "libtaint.so"] + sorted(build_dir.glob("libtaint.so.*"))

    source = next((candidate for candidate in candidates if candidate.exists()), None)
    if source is None:
        fail(f"could not find a built library in {build_dir}")

    output_dir = OUTPUT_ROOT / rid
    output_dir.mkdir(parents=True, exist_ok=True)
    output = output_dir / library_name
    shutil.copyfile(source.resolve(), output)
    print(f"staged {output} ({output.stat().st_size} bytes)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
