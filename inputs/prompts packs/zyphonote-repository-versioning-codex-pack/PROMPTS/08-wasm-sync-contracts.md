Harden the API/contracts for offline-first WASM usage.

Required work:
1. Ensure origin status batch works for multiple repos.
2. Ensure pull batch returns enough graph/ref data for offline history.
3. Ensure commit/push DTOs are deterministic and hash-verifiable.
4. Document or implement DTOs suitable for IndexedDB.
5. Avoid browser-local assumptions that only work with localStorage.

Important:
- the server is still origin/source-of-truth
- local commits may exist offline, but remote ref updates must be validated server-side

Update checklists after completion.
