"""Focused regression tests for API-reference rendering helpers."""

from __future__ import annotations

import importlib.util
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
            ],
            check=True,
            capture_output=True,
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
    print(f"validated {len(cases)} cref labels")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
