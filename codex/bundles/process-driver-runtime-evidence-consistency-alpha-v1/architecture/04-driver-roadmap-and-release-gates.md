# Driver Roadmap And Release Gates

## After this bundle
If this bundle passes, the system should have:
- refactored transcript verification alpha,
- runtime evidence consistency verifier alpha,
- process read-only adapters for both alphas,
- stronger shared verification invariants,
- Core/driver consumer allow-lists,
- no generic runtime host.

## Next possible bundle after this one
A production `VerificationOnly` host proposal may be considered only if:
- two verification-only alphas remain stable,
- both process adapters prove no mutation,
- audit/redaction/no-mutation semantics are shared,
- runtime host design is still explicit and not accidental,
- no registry/DI/manager command has been introduced prematurely.

## Still denied
- execution-capable domain drivers,
- shell execution,
- package restore,
- Office/Graph calls,
- business record mutation,
- workspace/storage writes,
- process mutation,
- claim/transition/finalizer/retry ownership.

## Domain lane order
1. `.NET/Rust` transcript verifier: active alpha.
2. Runtime evidence consistency verifier: this bundle.
3. Business-analysis read-only verifier: future, over supplied deliverables only.
4. Office read-only verifier: future, over supplied evidence only, no Graph/mail/task/doc mutation.
5. Execution-capable drivers: much later after sandbox/allowlist/audit persistence approval.
