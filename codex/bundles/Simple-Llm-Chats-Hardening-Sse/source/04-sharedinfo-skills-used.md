# SharedInfo architecture guidance used

The review and bundle structure use the following material from
`fyziktom/CanDoItAll.SharedInfo` `main`:

- `candoitall-bundle-preparation`
- `candoitall-bundle-execution`
- `candoitall-csharp-architecture-bundle-guard`
- `csharp-architecture-governor`
- `feature-block-architecture-review`
- `canonical-model-review`
- `_csharp-architecture-shared` references for dependency direction, responsibility slicing,
  pattern selection, partial-class policy, proof, and testability

## Applied principles

- Canonical truth must have one writable owner.
- A façade, callback, partial class, or interface does not create real modularity by itself.
- Transactions and profile fences are correctness boundaries, not naming conventions.
- Application behavior must be testable without constructing unrelated runtimes.
- Provider protocol details remain in provider drivers.
- Composition roots wire behavior but do not own it.
- Critical foundations precede feature work and unlock it only after checkpoint proof.
- Tests are selected by affected risk and behavior; broad suites are final closure evidence.
