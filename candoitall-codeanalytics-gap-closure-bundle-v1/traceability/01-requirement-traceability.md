# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `finding-01-solution-inventory-mixes-product-and-test-projects.md` | `requirements/01-normalized-requirements.md`, `analysis/01-current-state.md` | `subbundles/01-project-inventory-classification-and-filtering` | `dotnet test C:\repositories\CanDoItAll.CodeAnalsis\tests\CanDoItAll.CodeAnalytics.Tests.Unit\CanDoItAll.CodeAnalytics.Tests.Unit.csproj --no-restore` plus installed-server rerun | Critical foundation for Scenario 1 precision. |
| `finding-02-legacy-focused-context-behavior-intent-alias-fails.md` | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` | `subbundles/02-focused-context-legacy-intent-compatibility` | Focused-context alias proof through installed MCP plus regression proof for `TroublePath` | Narrow compatibility fix only. |
| `REQ-06 reinstall and rerun closure` | `plan/01-phase-plan.md`, `reviews/01-execution-report.md` | `subbundles/03-reinstall-rerun-and-closure` | Host build, reinstall, rerun scorecard, bundle validators | Depends on subbundles 01 and 02. |
