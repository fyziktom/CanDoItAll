# Template Contract Migration Checklist

- [ ] Add or validate execution class for each process step.
- [ ] Add or validate deterministic tool plan metadata where scaffold/wire/readback is deterministic.
- [ ] Add or validate subprocess contract metadata for runtime-owned child runs.
- [ ] Add or validate accepted child output and no-go child output metadata.
- [ ] Add or validate required tool receipt metadata for proof-critical tools.
- [ ] Add or validate product path/readback/content gates.
- [ ] Add or validate artifact slot and ledger acceptance metadata.
- [ ] Remove or demote prose-only hard gates after typed equivalents exist.
- [ ] Update template validation tests for missing required typed metadata.
- [ ] Update assignment/capability tests so generic agents cannot receive deterministic tool-plan work without capability proof.
