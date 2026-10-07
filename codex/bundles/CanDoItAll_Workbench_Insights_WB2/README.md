# Workbench Insights and Selection WB2

A larger continuation after completed Workbench Planning WB1. First close one bounded
Gantt interop cleanup gap in its Components owner; then extract the **entire Manager
Summary/activity family and complete Structure selection/support windows**.

Start with [prompt.md](prompt.md). The [WB1 review](WB1_REVIEW.md) distinguishes
source review from the implementer's execution evidence. The [scope](SCOPE_AND_ARCHITECTURE.md),
[reporting design](REPORTING_AND_ACTIVITY.md), [selection design](SELECTION_AND_SUPPORT.md)
and [native boundaries](NATIVE_OWNERS_AND_CONTEXT.md) form the implementation brief.
Use the [validation matrix](VALIDATION_MATRIX.md), [native journeys](APPLICATION_JOURNEYS.md)
and [delivery procedure](EXECUTION_AND_CLOSURE.md) to close the whole slice.

This is not a main-canvas/runtime rewrite, API-only migration or release certification.
The 22-file [shared v3 foundation](shared/README.md) is preserved unchanged. Historical
source references and module maps inside it are not the current execution plan.

[Česká revize](REVIEW.cs.md) · [Source register](SOURCES.md) · [Signing](COMMITS_AND_SIGNING.md)

Package integrity: `python tools/validate_handoff.py`.
Tool self-tests: `python -m unittest discover -s tools -p "test_*.py"`.
These commands do not build or test CanDoItAll.
