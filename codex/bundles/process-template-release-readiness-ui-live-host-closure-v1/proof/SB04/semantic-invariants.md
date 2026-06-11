# SB04 Semantic Invariants

- Invariant ID: `SB04-INV-001`
- Source raw note: REQ-004 project/project-structure multi-team launch.
- Expected behavior: Project structure can launch the canonical process template and read back the project-scoped run detail and steps at large desktop size.
- Disallowed shallow implementation: Template selection alone or a non-project-scoped run detail page is insufficient.
- Failing-first test: The Playwright test fails if the run detail or expected steps do not appear.
- Passing test: `Project_structure_process_template_launch_SB02_INV_001_launches_approved_template_from_structure_context_and_reads_back_run`
- Changed source files: `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessStartSmoke.cs`
- Production assertions: Browser proof verifies project-structure context, launch confirmation, assignment review, redirect, and run steps.
- Red-team negative case: Alias drift and shortcut launch behavior are rejected by source scan and route assertions.
- Downstream dependency check: SB07 reuses this large desktop proof for the matrix.
