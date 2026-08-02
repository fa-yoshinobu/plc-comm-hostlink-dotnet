#!/usr/bin/env python3
"""Regression tests for lifecycle and safety contracts in user documentation."""

from __future__ import annotations

import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
USAGE = (ROOT / "docsrc/user/USAGE_GUIDE.md").read_text(encoding="utf-8")
OPTIONS_SOURCE = (
    ROOT / "src/PlcComm.KvHostLink/KvHostLinkConnectionOptions.cs"
).read_text(encoding="utf-8")
API_REFERENCE = (ROOT / "docsrc/user/API_REFERENCE.md").read_text(encoding="utf-8")


class DocumentationExamplesTests(unittest.TestCase):
    def test_waiting_and_active_cancellation_have_distinct_lifecycle_text(self) -> None:
        self.assertIn("Cancelling a waiting operation sends nothing", USAGE)
        self.assertIn("open transport\ngeneration remains usable", USAGE)
        self.assertIn("active-operation cancellation", USAGE)

    def test_general_buffer_examples_are_read_only_and_explain_reconciliation(
        self,
    ) -> None:
        active_lines = {
            line.strip()
            for line in USAGE.splitlines()
            if not line.lstrip().startswith("//")
        }
        self.assertFalse(
            any(
                line.startswith("await client.WriteExpansionUnitBufferAsync(")
                for line in active_lines
            )
        )
        self.assertFalse(
            any(line.startswith("await client.WriteAsync(") for line in active_lines)
        )
        self.assertIn("outcome-unknown failure", USAGE)
        self.assertIn("reconcile the actual PLC state", USAGE)

    def test_init_only_profile_is_described_as_initialization(self) -> None:
        self.assertIn(
            "Gets or initializes the canonical KEYENCE KV PLC profile", OPTIONS_SOURCE
        )
        self.assertIn("public string PlcProfile { get; init; }", API_REFERENCE)
        self.assertIn(
            "Gets or initializes the canonical KEYENCE KV PLC profile", API_REFERENCE
        )


if __name__ == "__main__":
    unittest.main()
