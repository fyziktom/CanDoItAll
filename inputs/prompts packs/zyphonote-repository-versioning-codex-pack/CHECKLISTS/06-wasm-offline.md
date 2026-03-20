# WASM offline checklist

- [x] IndexedDB is the primary repo storage model in the contract.
- [x] localStorage is limited to tiny UI state.
- [x] Refs, commits, snapshots, blobs, and working copies have DTOs.
- [x] Ahead/behind/diverged states are explicit.
- [x] Local branch checkout behavior is defined.
- [x] Offline commit creation can be verified by server canonicalization rules.
