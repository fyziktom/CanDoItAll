## Repeat offender summary

- **Resolved** — Persisted synchronized projection truth / SyncGraph-style mirrored rows: Resolved through ProjectStructureAssemblyService + integration tests that assert projection-only artifacts are not persisted.
- **Open** — Node carrier / binding split incomplete: Improved but still physically leaks through ProjectObjectRecord and metadata contract.
- **Open** — Dual hierarchy truth: Still repeated: ParentNodeKey + persisted hierarchy link rows.
- **Open** — Capability rules outside registry: Still repeated in workbench UI and CRM/HR validation.
- **Open** — Plugin seam partly legacy-enum driven: Improved by manifests/registries but not fully solved in provider/resource pages and domain models.
