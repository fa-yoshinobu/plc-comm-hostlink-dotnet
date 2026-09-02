"""Focused regression tests for API-reference rendering helpers."""

from __future__ import annotations

import importlib.util
import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path


sys.dont_write_bytecode = True
SCRIPT = Path(__file__).with_name("generate_api_reference.py")
SPEC = importlib.util.spec_from_file_location("generate_api_reference", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {SCRIPT}")
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)


BUILD_ENVIRONMENT = os.environ.copy()
BUILD_ENVIRONMENT["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0"


def build_fixture(root: Path, source: str) -> Path:
    root.mkdir()
    (root / "OrderingFixture.csproj").write_text(
        '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
        "<TargetFramework>net8.0</TargetFramework>"
        "<Nullable>enable</Nullable>"
        "<EnableNETAnalyzers>false</EnableNETAnalyzers>"
        "<TreatWarningsAsErrors>false</TreatWarningsAsErrors>"
        "</PropertyGroup></Project>",
        encoding="utf-8",
    )
    (root / "OrderingFixture.cs").write_text(source, encoding="utf-8")
    subprocess.run(
        [
            "dotnet",
            "build",
            root / "OrderingFixture.csproj",
            "-c",
            "Release",
            "--nologo",
            "-p:UseSharedCompilation=false",
        ],
        check=True,
        capture_output=True,
        env=BUILD_ENVIRONMENT,
        text=True,
        encoding="utf-8",
        errors="replace",
    )
    return root / "bin" / "Release" / "net8.0" / "OrderingFixture.dll"


def main() -> int:
    cases = {
        "M:Example.Client.OpenAsync(System.Threading.CancellationToken)": "OpenAsync",
        "M:Example.Client.Parse(Example.ResolvedDevice)": "Parse",
        "T:Example.Client": "Client",
        "P:Example.Client.Timeout": "Timeout",
        "M:Example.Client.Convert``1(System.UInt16)": "Convert",
    }
    for cref, expected in cases.items():
        actual = MODULE.cref_label(cref)
        if actual != expected:
            raise AssertionError(
                f"cref_label({cref!r}) returned {actual!r}, expected {expected!r}"
            )
        if ")" in actual:
            raise AssertionError(
                f"cref_label({cref!r}) retained a parameter-list suffix"
            )
    scratch_root = SCRIPT.parents[1] / "local_folder"
    scratch_root.mkdir(exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix="api-property-fixture-", dir=scratch_root
    ) as temp_dir:
        fixture = Path(temp_dir)
        (fixture / "PropertyFixture.csproj").write_text(
            '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup>'
            "<TargetFramework>net8.0</TargetFramework>"
            "</PropertyGroup></Project>",
            encoding="utf-8",
        )
        (fixture / "PropertyFixture.cs").write_text(
            "namespace GeneratorFixture;\n"
            "public sealed class PropertyFixture\n"
            "{\n"
            "    public int InitOnly { get; init; }\n"
            "    public int Mutable { get; set; }\n"
            "}\n",
            encoding="utf-8",
        )
        subprocess.run(
            [
                "dotnet",
                "build",
                fixture / "PropertyFixture.csproj",
                "-c",
                "Release",
                "--nologo",
                "-p:UseSharedCompilation=false",
            ],
            check=True,
            capture_output=True,
            env=BUILD_ENVIRONMENT,
            text=True,
            encoding="utf-8",
            errors="replace",
        )
        api = MODULE.run_inspector(
            fixture / "bin" / "Release" / "net8.0" / "PropertyFixture.dll"
        )

    members = {
        member["Name"]: member["Signature"]
        for api_type in api
        if api_type["Name"] == "PropertyFixture"
        for member in api_type["Members"]
    }
    if members.get("InitOnly") != "public int InitOnly { get; init; }":
        raise AssertionError(
            f"init-only property rendered incorrectly: {members.get('InitOnly')!r}"
        )
    if members.get("Mutable") != "public int Mutable { get; set; }":
        raise AssertionError(
            f"mutable property rendered incorrectly: {members.get('Mutable')!r}"
        )

    stable_a = """
using System.Threading.Tasks;
namespace GeneratorFixture;
public enum StableMode { Zebra = 2, Alpha = 1 }
public sealed class StableSurface
{
    public string Zebra(int value) => value.ToString();
    private async Task HiddenFirstAsync() { await Task.Yield(); }
    public async Task<int> EchoAsync(int value) { await Task.Yield(); return (int)value; }
    public int Alpha { get; init; }
}
"""
    stable_b = """
using System.Threading.Tasks;
namespace GeneratorFixture;
public enum StableMode { Alpha = 1, Zebra = 2 }
public sealed class StableSurface
{
    private async Task HiddenSecondAsync() { await Task.Yield(); }
    public int Alpha { get; init; }
    public async Task<int> EchoAsync(int value) { await Task.Yield(); return (int)value; }
    private async Task HiddenFirstAsync() { await Task.Yield(); }
    public string Zebra(int value) => value.ToString();
}
"""
    changed = stable_b.replace("EchoAsync(int value)", "EchoAsync(long value)")
    with tempfile.TemporaryDirectory(
        prefix="api-ordering-fixture-", dir=scratch_root
    ) as temp_dir:
        fixture_root = Path(temp_dir)
        api_a = MODULE.run_inspector(build_fixture(fixture_root / "a", stable_a))
        api_b = MODULE.run_inspector(build_fixture(fixture_root / "b", stable_b))
        api_changed = MODULE.run_inspector(
            build_fixture(fixture_root / "changed", changed)
        )

    if api_a != api_b:
        raise AssertionError(
            "public API rendering changed with declaration order or private async state machines"
        )
    rendered = json.dumps(api_a, ensure_ascii=False)
    if "d__" in rendered or "DisplayClass" in rendered:
        raise AssertionError("compiler-generated member names leaked into public API rendering")
    if api_a == api_changed:
        raise AssertionError("a public parameter-type change was normalized away")

    print(
        f"validated {len(cases)} cref labels, stable semantic ordering, "
        "compiler-generated exclusion, and public-signature negative control"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
