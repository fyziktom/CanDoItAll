# Gate B Header Snapshot Parity Source Assertions

- Invariant ID: SB08-INV-001
- Source proof: changed source files are listed in `proof/SB08/manifest.md` with SHA-256 hashes.
- Assertion: LoadDispatchCandidateHeadersAsync delegates candidate ordering and filtering to ProcessDispatchCandidateHeaderSelector.SelectAsync.
- Assertion: LoadDispatchCandidateAsync consumes ProcessDispatchCandidateHydrationLoader.LoadAsync for readback while side-effectful binding and recovery behavior remain outside the loader.
