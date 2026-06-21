# SB31 Semantic Invariants

- Process runtime event sequencing remains database-owned: EF/runtime code no longer hand-allocates `GlobalSequence`, and the repair migration advances the PostgreSQL sequence past existing rows.
- A process step cannot bind to an agent only because of a fuzzy name match. A candidate must have an enabled structured-output-capable provider, role-family fit, and workspace rights required by the step operations and target scope.
- Project-structure manual executor overrides are advisory selections, not bypasses. The launch resolver rejects invalid, unavailable, provider-incompatible, role-mismatched, or tool-incomplete overrides before run dispatch.
- Product-mutable .NET/Blazor steps require software-development readiness, including validation/runtime and scaffold capability when the operation contract allows product mutation.
- Read-only architecture, QA, security, delivery, and externally controlled subprocess steps remain explicitly non-mutating and may use narrower workspace profiles.
- Greenfield .NET project-structure targets are represented as typed scaffold contracts. The architecture classifier may classify missing/empty roots as greenfield, but only product-mutable setup/implementation steps may create product files.
- Managed process artifacts and external product roots remain separate: classification and architecture steps write managed evidence only; generated app files stay under the grounded product/output root.
