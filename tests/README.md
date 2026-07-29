# Tests

| Area | Responsibility |
|---|---|
| [Unit](Unit/CanDoItAll.Tests.Unit/README.md) | Fast domain, service, policy, and regression tests |
| [Integration](Integration/CanDoItAll.Tests.Integration/README.md) | Host, API, persistence, provider, plugin, and runtime integration |
| [Components](Components/CanDoItAll.Tests.Components/README.md) | Blazor component behavior and rendering |
| [Playwright](Playwright/CanDoItAll.Tests.Playwright/README.md) | Browser behavior at the supported desktop viewport |
| [Memory](Memory/CanDoItAll.Memory.Tests/README.md) | Memory contracts, drivers, persistence, and end-to-end behavior |
| [MAF](MAF/CanDoItAll.AgentFramework.Memory.Tests/README.md) | AgentFramework Memory integration |
| [Support](Support/CanDoItAll.Tests.Support/README.md) | Shared test host, PostgreSQL, file-system, and environment fixtures |

Use the routine gate in [Testing](../docs/testing.md). Run environment-dependent lanes
only when their prerequisites are available and report skipped or quarantined coverage
explicitly.
