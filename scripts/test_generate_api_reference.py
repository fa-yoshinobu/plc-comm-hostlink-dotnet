"""Focused regression tests for API-reference rendering helpers."""

from __future__ import annotations

import importlib.util
import sys
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
            raise AssertionError(f"cref_label({cref!r}) returned {actual!r}, expected {expected!r}")
        if ")" in actual:
            raise AssertionError(f"cref_label({cref!r}) retained a parameter-list suffix")
    print(f"validated {len(cases)} cref labels")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
