# Normalized requirements

R1. The active structure read seam must be zero-write.

R2. Stale projection cleanup must move to an explicit repair / maintenance boundary that is not reachable from structure reads.

R3. The repo must contain behavior tests that prove zero-write reads under stale projection rows, stale layout rows, and legacy compatibility payloads.

R4. The static gate must fail on the current false-green scenario by detecting direct or transitive persistence mutations in `LoadAsync(...)`.

R5. Future plugin-wave readiness must include unknown-manifest editor coverage across all shared field types.

R6. Remaining legacy metadata fallbacks for markers/references may stay as read-only compatibility seams during phase10, but they must stay visible as warnings and must not be expanded into new write-on-read behavior.
