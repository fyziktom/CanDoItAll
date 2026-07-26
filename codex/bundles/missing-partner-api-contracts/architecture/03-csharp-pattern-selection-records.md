# C# Pattern Selection Records

## PSR-01 Adapter: Portable Structured Output

- Force: public JSON cannot expose `.NET Type`, while internal providers already consume
  the existing runtime contract.
- Selected: transport adapter from versioned portable JSON Schema DTO to a runtime schema
  contract plus validator.
- Rejected: replace every internal typed contract in one breaking migration.
- Test seam: validate/convert JSON Schema without constructing the runtime.

## PSR-02 Strategy: Import Modes

- Force: `create`, `replace-exact-version`, and `clone` have different identity/concurrency
  decisions but share archive validation.
- Selected: closed import-mode strategy/policy over one inspected package.
- Rejected: endpoint switch containing validation and persistence.
- Test seam: mode policy receives inspected package metadata and catalog state.

## PSR-03 Command/Ledger: External Idempotency

- Force: concurrent retries need atomic fingerprint/result claims.
- Selected: durable idempotency command claim owned by the same catalog/run transaction.
- Rejected: process-memory cache or name-based duplicate check.
- Test seam: parallel claims with identical and conflicting fingerprints.

## PSR-04 State Derivation: Recruiting Readiness

- Force: readiness depends on immutable attempts and human reviews but must not activate an
  agent.
- Selected: pure readiness projection over evidence records with an explicit human gate.
- Rejected: mutable `IsProductionReady` flag set by automated evaluation.
- Test seam: incomplete, automated-only, rejected, and human-approved evidence sets.

## PSR-05 Facade: Existing Workspace API

- Force: current UI/runtime callers use broad workspace interfaces.
- Selected: thin compatibility facade delegating to focused services.
- Rejected: place all new behavior into another partial class.
- Proof: direct tests instantiate the focused service; facade contains no policy.
