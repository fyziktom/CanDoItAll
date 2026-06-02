# Hardening Quality Gates

## Gate Classes

### G1: Structural Gate

- Solution builds.
- Existing unit/integration/component tests pass or have documented, unrelated pre-existing failures.
- New services have targeted tests.
- No large refactor lands without characterization tests.

### G2: Canonical Contract Gate

- Every concept in the canonical inventory has one owner or an explicit external boundary.
- Internal string ids are wrapped by constants/descriptors.
- JSON paths are centralized where possible.
- UI display adapters translate numeric enum API shape into meaningful labels.
- Drift scanner reports no newly introduced unowned ids in scoped files.

### G3: Semantic Adequacy Gate

Required for critical subbundles:

- Shallow-pass trap is named.
- Adversarial negative proof rejects the shallow implementation.
- Semantic positive proof proves realistic behavior.
- Anti-stub audit scans production code for `TODO`, `NotImplemented`, fixture-specific branches, template-only output, and fake proof.
- Raw request closure is literal and does not narrow "all", "must", or "generic".

### G4: Artifact-Backed Proof Gate

Critical subbundles must write:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- command transcripts
- changed-file hashes
- source assertions
- anti-stub audit output
- browser/host artifacts where relevant
- red-team/verifier artifacts for final closure

### G5: Runtime Evidence Gate

Runtime proof must include:

- host URL
- database profile
- process run id
- process step id
- execution run id
- workflow run id when relevant
- artifact root
- command transcript path
- cleanup receipt path
- screenshot path when UI/browser proof is required

### G6: Usage Ledger Gate

Token/cost proof must include:

- usage observation rows or exported DTOs
- provider response ids where available
- known input/cached/output/reasoning/total tokens
- usage status for missing/null usage
- cost calculation transcript
- old-vs-new process cost comparison
- negative tests for finalizer/failure/repair/background undercount

### G7: External Side-Effect Gate

Workflow side-effect proof must include:

- dry-run behavior
- commit behavior in controlled test scope
- idempotency key
- duplicate prevention
- processed marker lifecycle
- unavailable executor diagnostics
- no accidental rerun of real evidence categories

### G8: Browser Proof Gate

Browser proof must include:

- Playwright MCP actions
- target route
- viewport
- screenshot path
- console messages
- interaction proof
- result
- reviewer notes answering layout/readability/behavior questions
- current-run binding

### G9: Final Red-Team Gate

The final verifier must attempt to falsify:

- stale-run artifact acceptance
- copied screenshot proof
- usage-zero finalizer path
- failed-run usage loss
- external mailbox duplicate processing
- hidden provider fallback
- Tetris-specific app generation assumptions
