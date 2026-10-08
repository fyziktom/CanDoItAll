# UI Decoupling Shared Bundle v4

**ID:** `CDA-UI-DECOUPLING-SHARED-v4` · **Reviewed:** 2026-10-08  
**Purpose:** one reusable execution companion, not an independent implementation assignment.

Read this with the selected child bundle and current repository instructions. The companion is designed for a substantial, staged Codex GPT-6 Astra Max assignment. Complete the selected feature family without negotiating a new task after each checkpoint; preserve every shipped capability and its native authority.

## Entry

1. Follow [migration](MIGRATION_FROM_V3.md) when replacing the active v3 directory. Keep the two delivered directories beside each other under `codex/bundles`.
2. Read the selected child's `prompt.md`, this [shared prompt](prompt.md), [execution protocol](EXECUTION.md), and [validation](VALIDATION.md).
3. Consult the four architecture documents for the affected boundary. The child's sources and feature matrix define implementation scope; historical bundles are context only.

Current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/testing.md`, evaluated project configuration and current CI are authoritative. This companion does not override repository security or ownership rules. Resolve a conflict explicitly, never silently choose an easier gate.

## What v4 changes

The v3 architecture was already substantially sound. This revision updates its stale module map, stops recursive/shared-copy accumulation, makes long-run checkpoints explicit, requires impact selection to include known blind spots, and ties closure to feature coverage and the actual source/dependency candidate. It consolidates lessons from 31 historical entry briefs rather than treating their old tasks as new work. See [change log](CHANGELOG.md) and [history](audit/bundle-history.md).

The [current map](audit/module-map.md) separates remaining extraction from native owner code and outstanding validation. The [source register](audit/sources.json) records complete versus partial reads and metadata-only observations. Its SHAs are review provenance, **never checkout/reset instructions**.

## Read-only helpers

```text
python -B tools/handoff_tools.py verify .
python -B tools/handoff_tools.py verify ../CanDoItAll_Processes_UI_Decoupling_PC1 --shared .
python -B tools/handoff_tools.py inspect --repo ../../.. --sources audit/sources.json --output <owned-path>/entry-inspection.json
python -B -m unittest discover -s tools -p "test_*.py"
```

Use these paths only after installing this directory at `codex/bundles/UI_Decoupling_Shared_Bundle`. Before installation, pass the actual repository path. `inspect` is a read-only drift/candidate census, not a semantic architecture guard. It does not invoke the MCP, build .NET, mutate the checkout, or prove product behavior. A drift report calls for review, not a reset. Package checks and their actual results are described in [package validation](PACKAGE_VALIDATION.md).


### Helper prerequisites and exit codes

Use Python 3.10 or newer. `inspect` additionally requires Git and a local checkout with a valid HEAD; it performs no network access. Choose an existing task-owned output directory. An output file is created exclusively and is never overwritten.

`verify` returns 0 for package integrity success, 1 for an integrity/companion mismatch, and 2 for a tool/input error. `inspect` returns 0 when its checked paths show no drift, 3 when drift requires review, and 2 for an inspection/input error. Exit 3 is not an application failure and never authorizes checkout/reset. Neither exit 0 nor the source register establishes semantic closure, source authenticity or product correctness.

All new package and repository content is English. Human conversation may be Czech. No credentials, private product evidence, font files or compiled application artifacts are included.
