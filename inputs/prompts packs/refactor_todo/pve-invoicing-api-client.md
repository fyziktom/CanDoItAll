# Status: Completed

This refactor was completed.

Implemented changes:
- `Helpers/PveInvoicingApiClient` now supports an injected `HttpClient` constructor.
- Existing constructor behavior was preserved.
- Unit tests now cover deterministic request composition and response mapping without live HTTP.

Verification:
- Covered by `PVEInvoicing/PVEInvoicing.Tests/Unit/Helpers/PveInvoicingApiClientTests.cs`.
