#!/usr/bin/env python3
"""Self-tests for the successor LLM Chat architecture guard."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import tempfile
import unittest


SCRIPT = Path(__file__).with_name("check_architecture_boundaries.py")
SPEC = importlib.util.spec_from_file_location("llm_chat_architecture_guard", SCRIPT)
assert SPEC is not None and SPEC.loader is not None
GUARD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(GUARD)


class ArchitectureGuardTests(unittest.TestCase):
    def test_partial_class_record_and_struct_are_rejected(self) -> None:
        for declaration in (
            "internal partial class EndpointOwner {}",
            "public partial record AuditRow();",
            "public partial struct Cursor {}",
        ):
            with self.subTest(declaration=declaration):
                self.assertTrue(GUARD.contains_partial_declaration(declaration))

    def test_non_partial_distinct_types_are_accepted(self) -> None:
        self.assertFalse(
            GUARD.contains_partial_declaration(
                "internal static class Definitions {}\ninternal sealed record Cursor();"
            )
        )

    def test_forbidden_dependency_tokens_are_reported_exactly(self) -> None:
        text = (
            "using CanDoItAll.Web;\n"
            "using Microsoft.EntityFrameworkCore;\n"
            "using CanDoItAll.AgentFramework.Models;"
        )
        self.assertEqual(
            ["CanDoItAll.Web", "Microsoft.EntityFrameworkCore"],
            GUARD.forbidden_tokens(text, GUARD.FORBIDDEN_CORE_REFERENCES),
        )

    def test_project_reference_names_are_strongly_typed_by_project_stem(self) -> None:
        project = """<Project Sdk=\"Microsoft.NET.Sdk\">
  <ItemGroup>
    <ProjectReference Include=\"..\\Core\\CanDoItAll.Modules.LlmChats.csproj\" />
    <ProjectReference Include=\"../Models/CanDoItAll.AgentFramework.Models.csproj\" />
  </ItemGroup>
</Project>"""
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "Fixture.csproj"
            path.write_text(project, encoding="utf-8")
            self.assertEqual(
                {
                    "CanDoItAll.Modules.LlmChats",
                    "CanDoItAll.AgentFramework.Models",
                },
                GUARD.project_reference_names(path),
            )

    def test_shadow_dispatch_patterns_are_rejected(self) -> None:
        for text in (
            "Channel<Operation> queue;",
            "ConcurrentQueue<Operation> queue;",
            "Task.Run(() => Dispatch());",
        ):
            with self.subTest(text=text):
                self.assertIsNotNone(GUARD.SHADOW_DISPATCH.search(text))

    def test_bounded_worker_fanout_is_not_a_shadow_queue(self) -> None:
        text = "Enumerable.Range(1, count).Select(RunWorkerAsync).ToArray();"
        self.assertIsNone(GUARD.SHADOW_DISPATCH.search(text))

    def test_sensitive_public_operation_fields_are_rejected(self) -> None:
        for field in ("RequestFingerprint", "ProviderBody", "ApiKey", "SystemPrompt"):
            with self.subTest(field=field):
                self.assertIsNotNone(GUARD.FORBIDDEN_OPERATION_DTO_FIELDS.search(field))

    def test_public_raw_inner_exception_is_rejected_in_any_constructor_position(self) -> None:
        unsafe = """throw new LlmInvocationException(
    LlmInvocationFailureKind.DeadlineExceeded,
    provider.Name,
    model,
    correlationId,
    exception,
    aggregateUsage);"""
        safe = """throw new LlmInvocationException(
    LlmInvocationFailureKind.DeadlineExceeded,
    provider.Name,
    model,
    correlationId,
    usage: aggregateUsage);"""

        self.assertIsNotNone(GUARD.PUBLIC_RAW_INNER_EXCEPTION.search(unsafe))
        self.assertIsNone(GUARD.PUBLIC_RAW_INNER_EXCEPTION.search(safe))


if __name__ == "__main__":
    unittest.main()
