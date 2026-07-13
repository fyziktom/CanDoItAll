# Planned Proof Manifests

Each subbundle must create `proof/<subbundle>/manifest.md` during implementation.

Required manifest sections:

- Changed files.
- Behavior moved out of adapter.
- Tests added or updated.
- Test transcript.
- Build transcript.
- CodeAnalytics snapshot id and dependency/cycle result when relevant.
- Source assertions.
- Partial-class policy proof.
- Domain-boundary source assertion.
- Risks left open.

Critical subbundles must also include:

- Before/after dependency proof.
- Before/after adapter line-count or responsibility proof.
- Direct unit tests for extracted behavior.
- Negative test transcript.
- No-new-partial proof.
- Statement that production path uses the extracted service.

