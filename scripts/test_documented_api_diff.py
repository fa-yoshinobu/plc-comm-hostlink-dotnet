#!/usr/bin/env python3
"""Focused policy tests for the documented API-diff classification gate."""

from __future__ import annotations

import importlib.util
import json
import sys
import tempfile
from pathlib import Path


sys.dont_write_bytecode = True
SCRIPT = Path(__file__).with_name("check_documented_api_diff.py")
SPEC = importlib.util.spec_from_file_location("check_documented_api_diff", SCRIPT)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {SCRIPT}")
MODULE = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = MODULE
SPEC.loader.exec_module(MODULE)

FRAMEWORKS = {"net8.0", "net9.0", "net10.0"}
COMMIT = "1" * 40


def write(root: Path, relative: str, text: str = "evidence") -> None:
    path = root / relative
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(text, encoding="utf-8")


def differences_for_all_frameworks() -> list[object]:
    templates = [
        ("changed", "M:Example.Documented", "old-complete", "new-complete"),
        ("removed", "M:Example.Hidden", "old-hidden-complete", None),
        ("added", "T:Example.Added", None, "new-type-complete"),
        ("changed", "M:Example.Generated", "old-generated", "new-generated"),
    ]
    return [
        MODULE.Difference(framework, change, symbol, before, after)
        for framework in sorted(FRAMEWORKS)
        for change, symbol, before, after in templates
    ]


def classification(change: str, symbol: str, before: str | None, after: str | None, category: str, evidence: str) -> dict[str, object]:
    return {
        "frameworks": sorted(FRAMEWORKS),
        "change": change,
        "symbol": symbol,
        "before": before,
        "after": after,
        "category": category,
        "evidence": evidence,
    }


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="documented-api-policy-test-") as temp_name:
        root = Path(temp_name)
        write(root, "internal/migration.md", "DEC-DOCUMENTED")
        write(root, "CHANGELOG.md", "**Breaking:** exact migration")
        write(root, "docs/USAGE_GUIDE.md", "standard guide")
        write(root, "docs/API_REFERENCE.md", "generated guide")
        prior_contract = {
            "README": ["Example.Documented"],
            "standard-user-pages": ["stable public guide"],
            "generated-api-reference": ["M:Example.Documented"],
            "maintained-samples": ["maintained example"],
        }

        differences = differences_for_all_frameworks()
        config = {
            "schemaVersion": 2,
            "evidenceSets": {
                "documented": {
                    "decision": "DEC-DOCUMENTED",
                    "rationale": "approved documented break",
                    "migrationPath": "internal/migration.md",
                    "changelogPath": "CHANGELOG.md",
                    "changelogNeedle": "**Breaking:**",
                    "documentationEvidence": [
                        {"path": "docs/USAGE_GUIDE.md", "needle": "standard guide"},
                        {"path": "docs/API_REFERENCE.md", "needle": "generated guide"},
                    ],
                    "versionPolicy": "major-version-required-before-release",
                    "priorContractCommit": COMMIT,
                    "priorContractTerm": "Example.Documented",
                },
                "hidden": {
                    "decision": "DEC-HIDDEN",
                    "rationale": "exact symbol absent from every immutable prior contract scope",
                    "priorContractCommit": COMMIT,
                    "priorContractTerm": "Example.Hidden",
                },
                "additive": {
                    "decision": "DEC-ADDITIVE",
                    "rationale": "reviewed new surface",
                    "documentationDisposition": "documented",
                    "documentationPaths": ["docs/API_REFERENCE.md"],
                },
                "generated": {
                    "decision": "DEC-GENERATED",
                    "rationale": "one exact compiler-generated member",
                    "generator": "SyntheticGenerator.Member",
                },
            },
            "classifications": [
                classification("changed", "M:Example.Documented", "old-complete", "new-complete", "documented-contract", "documented"),
                classification("removed", "M:Example.Hidden", "old-hidden-complete", None, "undocumented-public", "hidden"),
                classification("added", "T:Example.Added", None, "new-type-complete", "additive", "additive"),
                classification("changed", "M:Example.Generated", "old-generated", "new-generated", "generated-or-noncontract", "generated"),
            ],
        }

        call = lambda cfg, version="2.0.0": MODULE.enforce_classifications(
            differences,
            cfg,
            root,
            baseline_version="1.4.0",
            candidate_version=version,
            require_release_version_policy=True,
            expected_frameworks=FRAMEWORKS,
            prior_contract_commit=COMMIT,
            prior_contract=prior_contract,
        )
        call(config)

        unclassified = json.loads(json.dumps(config))
        unclassified["classifications"].pop()
        try:
            call(unclassified)
        except MODULE.GateError as error:
            if "unclassified API differences" not in str(error):
                raise
        else:
            raise AssertionError("an unclassified framework-specific difference did not fail")

        bad_signature = json.loads(json.dumps(config))
        bad_signature["classifications"][0]["after"] = "different"
        try:
            call(bad_signature)
        except MODULE.GateError as error:
            if "classified signature drift" not in str(error):
                raise
        else:
            raise AssertionError("a changed full after-signature passed")

        try:
            call(config, version="1.9.0")
        except MODULE.GateError as error:
            if "require a major version" not in str(error):
                raise
        else:
            raise AssertionError("a same-major documented break passed release enforcement")

        duplicate_surface = [
            {"Id": "M:Example.Duplicate", "DeclaringType": "Example", "Signature": "a"},
            {"Id": "M:Example.Duplicate", "DeclaringType": "Example", "Signature": "b"},
        ]
        try:
            MODULE.diff_surfaces("net8.0", duplicate_surface, [])
        except MODULE.GateError as error:
            if "duplicate API ID" not in str(error):
                raise
        else:
            raise AssertionError("duplicate API IDs were silently collapsed")

        required_inspector_coverage = [
            "BindingFlags.NonPublic",
            "IsNestedFamily",
            "IsInitOnly(property)",
            "Enum.GetUnderlyingType(type)",
            "GetRawConstantValue()",
            'method.Name.StartsWith("op_"',
            "property.GetIndexParameters()",
            ".ProfileDescriptor(",
        ]
        for snippet in required_inspector_coverage:
            if snippet not in MODULE.CSHARP_INSPECTOR:
                raise AssertionError(f"API inspector coverage regressed: {snippet}")

    print("validated 3-TFM exact signatures, prior-contract classification, duplicate IDs, special surface coverage, and major release enforcement")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
