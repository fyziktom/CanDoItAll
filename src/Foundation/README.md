# Foundation

Foundation projects provide low-level capabilities shared by multiple application areas.

| Project | Responsibility |
|---|---|
| [CanDoItAll.SharedKernel](CanDoItAll.SharedKernel/README.md) | Shared typed primitives and cross-domain contracts |
| [CanDoItAll.Infrastructure](CanDoItAll.Infrastructure/README.md) | EF Core, storage, configuration, and external infrastructure implementations |
| [CanDoItAll.Migrations.PostgreSql](CanDoItAll.Migrations.PostgreSql/README.md) | Canonical PostgreSQL migrations |
| [CanDoItAll.Git](CanDoItAll.Git/README.md) | Validated Git command and repository access |

Foundation must not depend on Blazor pages or product-module UI.
