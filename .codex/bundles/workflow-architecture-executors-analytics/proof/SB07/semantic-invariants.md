# SB07 Closure Semantic Invariants

## Evidence-backed final closure

- Invariant ID: SB07-EVIDENCE-CLOSURE
- Source raw note: improve workflow architecture, implementation, test coverage, executor nodes, launch paths, analytics, and large-screen UI as one complete initiative.
- Expected behavior: every raw note has production and validation evidence, browser-visible requirements have reviewed 1600x1000 captures, and architecture closure uses the final scoped snapshot.
- Disallowed shallow implementation: mark completion from status tables, catalog counts, screenshots without visual review, or focused tests while a required progression gate remains pending.
- Failing-first test: the completed-stage validator must reject any remaining Ready/In progress status, pending report row, missing critical manifest/transcript, or weak raw-note proof.
- Passing test: the final scoped solution/unit/component/integration/PostgreSQL/EF validation passed and the completed-stage validator exited 0; see `bundle://proof/SB07/transcripts/closure.txt`.
- Changed source files: implementation ownership and hashes remain in SB01-SB06 manifests; SB07 changes closure evidence only.
- Production assertions: `bundle://proof/SB06/browser-validation.md` exercises the production catalog, trusted renderer, plugin schema, and analytics UI; `bundle://reviews/csharp-architecture-gate.md` records the final architecture graph.
- Red-team negative case: `bundle://proof/SB07/closure-verifier.md` explicitly rejects planned-status relabeling, missing browser review, fake descriptor-only executors, and prose-only architecture proof.
- Downstream dependency check: this is the terminal subbundle; a failed check reopens the owning SB01-SB06 gate rather than adding a miscellaneous SB07 implementation.
