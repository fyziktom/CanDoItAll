# Test Entrypoints

The product solution is not the test authority. Use lane workspaces under `tests/Solutions`.

| Area | Workspace | Default selection |
|---|---|---|
| Reusable/Blazor components | `tests/Solutions/CanDoItAll.Tests.Components.slnx` | exact classes/methods from impacted-test analysis |
| Pure application/state logic | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | exact class or namespace |
| PostgreSQL/Web/application integration | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | exact host/persistence cases |
| Browser behavior | `tests/Solutions/CanDoItAll.Tests.Playwright.slnx` | named scenario methods only |
| Broad final compatibility | `tests/Solutions/CanDoItAll.Tests.Stable.slnx` | unfiltered once in SB12 only |

Every test command must first list/discover the selector and fail when discovery is zero or differs materially from the expected named cases.
