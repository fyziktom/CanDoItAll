# Test gap backlog

## Highest-priority missing tests

### Canonical dependency model
- Add a test proving the canonical dependency representation survives save/load/publish/clone without reading legacy fields outside the compatibility adapter.
- Add a test proving a migrated or compatibility-loaded legacy definition is normalized once and then persisted canonically.

### Pure validation
- Add tests proving validation does not mutate the editor model.
- Add tests proving normalization is explicit and idempotent when called intentionally.

### Differential persistence
- Add a no-op save test that preserves child IDs.
- Add a single-step edit test that preserves unrelated child IDs.
- Add a targeted delete test that removes only intended children.
- Add rollback tests that prove partial child graph writes do not remain after a failure.

### Concurrency and conflicts
- Add two-context integration tests for conflicting definition save.
- Add two-context integration tests for conflicting publish.
- Add two-context integration tests for conflicting step transition.
- Add tests for slug/version uniqueness conflict translation into the module result/error pattern.

### Runtime extraction
- Add unit tests for transition guard/policy services.
- Add unit tests for dependent-activation planning and non-selected path resolution.
- Add regression tests for journal/improvement side effects after the extraction.

### Read side
- Add tests for definition list query shape or at least summary correctness without broad unnecessary graph loads.
- Add analytics tests that prove counts and totals remain correct after query service extraction.

### Consolidation
- Add tests for shared JSON loader behavior, enum parser defaults, and role snapshot summary builder parity.
- Update template tests so shared extraction is validated centrally.

### UI decomposition
- Keep existing component behaviors covered.
- Add or update focused tests for the extracted state container or smaller components if introduced.

## Proof strategy

These tests do not all need to land in one phase. The owning subbundle for each gap is listed in `traceability/02-finding-to-subbundle-map.md`.
