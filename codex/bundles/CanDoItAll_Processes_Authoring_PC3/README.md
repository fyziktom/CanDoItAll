# Processes PC3 — Durable native authoring and publication

**Assignment:** `CDA-PROCESSES-AUTHORING-PC3`  
**Package revision:** `1.1` — single-download handoff  
**Executor:** Codex GPT-6 Astra Max  
**Reviewed branch/head:** `components-decoupling` / `095c418b7981511ba9c96a75b7d2950ff4f326a6` (2026-10-09)  
**Shared baseline:** the existing `codex/bundles/UI_Decoupling_Shared_Bundle` in the current checkout; no required version or manifest pin.

Implement the next complete vertical slice: persist the authored definition and its
roles, steps, canvas structure/layout and imported components through one native owner;
read it consistently in the catalog/editors; publish immutable executable revisions; and
launch/recover the exact resolved content. Keep the extracted UI and all PC1/PC2 behavior.

PC2's in-scope corrections are substantively present. Its native-authoring prerequisite
was intentionally not implemented. **PC3 explicitly moves that bounded prerequisite into
implementation scope**, including an additive canonical PostgreSQL migration. This is not
another architecture-only exercise and not permission to redesign unrelated runtime/auth.

This revision changes handoff and shared-maintenance policy, not the product scope. The
50 PC3 acceptance groups and all 42 PC1 + 31 PC2 carry-forward obligations are retained.
It does not claim a new repository review or a new product test run.

Start with [prompt](prompt.md), [shared policy](SHARED_BUNDLE_POLICY.md),
[review](REVIEW_PC2.md) and [stages](SCOPE_AND_STAGES.md).
The detailed constraints are [owner design](NATIVE_OWNER_DESIGN.md),
[semantic coverage](SEMANTIC_COVERAGE.md), [observation contract](OBSERVATION_AND_MUTATIONS.md),
[launch integration](PUBLISH_AND_LAUNCH.md), [migration/scope](MIGRATION_AND_SCOPE.md)
and [validation](VALIDATION_PLAN.md). Use [browser journeys](BROWSER_JOURNEYS.md)
and the [acceptance plan](acceptance-plan.json) as execution obligations, not predetermined
passing results. [Sources](SOURCES.json) are navigation/provenance, not an exhaustive graph.

## Install this archive only

Extract the one directory from this ZIP into `codex/bundles`:

```text
codex/bundles/
    CanDoItAll_Processes_Authoring_PC3/   <- this archive
    UI_Decoupling_Shared_Bundle/         <- already in the repository; leave in place
```

Do not download, unpack, replace or upgrade the shared bundle as an installation step.
In particular, the previously offered shared v4.2 archive is not required. This ZIP contains
no shared snapshot, shared replacement files, shared installer or automatic shared patch.
If shared v4.2 was already installed, do not roll it back just for this assignment either;
use the current reviewed repository baseline.

If an earlier PC3 revision is already present, preserve any operator edits and runtime
evidence before replacing only the PC3 input directory. Do not blindly merge duplicate
instructions or overwrite unrelated work. Never replace the shared directory with this ZIP.

Verify this task package independently from the repository root:

```text
python -B codex/bundles/CanDoItAll_Processes_Authoring_PC3/tools/verify_package.py codex/bundles/CanDoItAll_Processes_Authoring_PC3
```

Give Codex this package's `prompt.md`. Codex reads the installed shared guidance itself,
requests local PGP unlock immediately and creates logical verified signed commits throughout
the run. Follow [SHARED_BUNDLE_POLICY](SHARED_BUNDLE_POLICY.md) for genuinely necessary
shared edits: Codex makes a minimal reviewed in-repository change, not a companion release
for the operator to install. The expected default is no shared changes.

No automatic push, merge, real-database migration, paid inference or deployment is authorized.
Work from the current checkout; the reviewed SHA is not an execution pin.

The previous reviewer inspected source and reports remotely, but did not execute .NET,
PostgreSQL, Playwright or CodeAnalytics. PC3 product evidence therefore starts `NOT_RUN`.
Package-only verification is described in [PACKAGE_VALIDATION](PACKAGE_VALIDATION.md).
