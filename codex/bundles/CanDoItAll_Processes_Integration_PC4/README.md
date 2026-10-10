# Processes integration PC4

**One task archive. Reuse the existing shared bundle; do not replace it.**

Retain PC3's durable authoring, immutable publications, request-owned reads, UI extraction,
canonical database owner and exact prepared launches. Implement the remaining producer and
upgrade compatibility fixes, resource lifecycle repair and source-addressed validation.
This is an implementation assignment, not another persistence design or renderer move.

Extract only `CanDoItAll_Processes_Integration_PC4` into `codex/bundles/`. Start with
[prompt.md](prompt.md). The review SHA is provenance, never an instruction to reset the
operator's checkout. Every repository edit, comment, test, commit and product document is
English. The operator conversation may be Czech.

## Read order

1. [Execution prompt](prompt.md), [shared policy](SHARED_BUNDLE_POLICY.md),
   [signing and checkpoints](SIGNING_AND_COMMITS.md).
2. [Review of PC3](REVIEW_PC3.md) and [stages](SCOPE_AND_STAGES.md).
3. [Producer consistency](PRODUCER_CONSISTENCY.md), [legacy recovery](LEGACY_RECOVERY.md),
   [role resources](ROLE_RESOURCE_LIFECYCLE.md).
4. [Validation](VALIDATION_PLAN.md), [browser journeys](BROWSER_JOURNEYS.md),
   [closure and residuals](CLOSURE_AND_RESIDUALS.md).
5. [Acceptance plan](acceptance-plan.json), [carry-forward matrix](CARRY_FORWARD.csv),
   [source register](SOURCES.json), [evidence template](evidence-template.json).

The acceptance entries are coverage groups, not mandatory test/class/commit counts.
Use the existing CodeAnalytics MCP to discover affected tests when available, supplemented
by actual Razor/DI/HTTP/serialization/fixture consumers and all new regressions.
Do not rerun the entire test suite after each stage. Read current repository gate policy.

## Package verification

```powershell
python codex/bundles/CanDoItAll_Processes_Integration_PC4/tools/verify_package.py
python -m unittest discover -s codex/bundles/CanDoItAll_Processes_Integration_PC4/tools -p "test_*.py"
```

These commands verify this instruction package only. They do not build or validate the
application. Product evidence starts as `NOT_RUN`. The reviewer performed source analysis,
not C# execution, PostgreSQL tests, Playwright, runtime profiling or a complete branch census.
See [package scope](PACKAGE_VALIDATION.md) for the exact limits.
