# UI Decoupling Shared Bundle v4.1

**ID:** `CDA-UI-DECOUPLING-SHARED-v4.1`  
**Review:** 2026-10-09, `components-decoupling`, `146067ed133f624878dfe5756c441a43c0f21b4a`  
**Purpose:** one execution companion, not an independent implementation assignment.

Use this companion with **PC2**. Preserve the completed PC1 renderer extraction; do not replay an old roadmap. The reviewed commit is provenance, never an instruction to reset the operator's checkout.

## Start

1. Install the two directories beside each other under `codex/bundles`, following [migration](MIGRATION_FROM_V4.md).
2. Read the child's `prompt.md`, this [shared contract](prompt.md), [execution protocol](EXECUTION.md), [signing protocol](SIGNING_AND_COMMITS.md), and [validation rules](VALIDATION.md).
3. Before implementation, request the operator's local PGP unlock, establish a verified signing session, inspect current source/sibling drift, and discover affected tests using the configured CodeAnalytics MCP.

Current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/testing.md`, evaluated configuration, and CI remain authoritative. These files do not waive security, ownership, signing, or validation requirements.

## Changes in v4.1

- Request signing access at entry; make logical tested commits throughout the assignment, not one final batch. Keep the same GPG agent/home and a dedicated committer environment. Cache expiry is explicit, not a promise that an open PowerShell window keeps a key unlocked forever.
- Distinguish a read generation from a pending mutation and its accepted receipt. A refresh must not erase an acknowledged write.
- Reconcile authoritative selections, hidden semantic fields, raw inputs, and concurrent edits by actual identity. Preserve completed PC1 draft and file-lifetime improvements.
- Commit a sanitized source-addressed verification summary. An ignored local ledger alone cannot support review of a pushed candidate.
- Mark all eight Processes renderer families extracted but functionally qualified; do not confuse preexisting authoring storage debt with a failed file extraction.

The [current map](audit/module-map.md) supersedes the explicitly historical [v4 map](audit/module-map-v4-historical.md). The [current source register](audit/sources.json) is a partial review navigation aid. The [31-entry historical survey](audit/bundle-history.md) remains attributed to the earlier review and was not repeated wholesale for v4.1.

## Read-only helpers

From this installed directory:

```text
python -B tools/handoff_tools.py verify .
python -B tools/handoff_tools.py verify ../CanDoItAll_Processes_UI_Decoupling_PC2 --shared .
python -B tools/handoff_tools.py inspect --repo ../../.. --sources audit/sources.json --output <owned-existing-directory>/entry-inspection.json
python -B -m unittest discover -s tools -p "test_*.py" -v
```

Use Python 3.10+; repository inspection also requires Git. `verify` returns 0 for integrity, 1 for a mismatch, 2 for an input/tool error. `inspect` returns 3 for source/worktree drift, not a product failure and not authorization to reset. Output files are created exclusively. Inspect private paths before sharing reports.

The helper performs no MCP query, .NET build, C# test, browser test, authentication, or product mutation. Hashes prove delivery consistency, not source authenticity or executed product behavior. See [package-only checks](PACKAGE_VALIDATION.md).

All repository and package content is English. No private keys, credentials, font binaries, compiled application files, or raw product traces belong in these packages.
