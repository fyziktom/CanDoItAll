# Test Matrix Template

| Layer | Scenario | Required Before Closure? | Command / Harness | Evidence |
| --- | --- | --- | --- | --- |
| Unit | Catalog CRUD and active-profile resolution | `Yes` | `dotnet test tests/CanDoItAll.Tests.Unit/...` | Log excerpt |
| Integration | SQLite ↔ PostgreSQL switch | `Yes` | `dotnet test tests/CanDoItAll.Tests.Integration/...` | Test name + result |
| Component | Startup modal / settings UI | `Yes` | `dotnet test tests/CanDoItAll.Tests.Components/...` | Test name + result |
| Playwright | Multi-tab switch / stale-route recovery | `Yes` | `dotnet test tests/CanDoItAll.Tests.Playwright/...` | Screenshot paths + assertions |
| External dependency | PostgreSQL available | `If required by scenario` | `docker compose up -d postgres` or documented remote target | Connection proof |
| External dependency | IPFS node available | `Optional real-node path` | Real-node command or configuration proof | CID / API log |
