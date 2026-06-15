# Transcript Verifier Decomposition

Split `TranscriptVerificationAlphaVerifier` into internal collaborators:
- request policy validator,
- evidence reference normalizer/hash validator,
- transcript parser abstraction with .NET/Rust parser implementations,
- redaction policy,
- audit fact builder,
- diagnostic factory.

Acceptance:
- public verifier behavior remains unchanged.
- existing alpha tests continue passing.
- new parser tests cover .NET warning/error, nullable, platform warnings, missing artifact, proof gap, Rust compile/test/clippy/panic/toolchain/missing artifact.
- source scans prove no IO/network/runtime hooks.
