Add repository API routes and security.

Required work:
1. Add repository discovery/status/pull/commit/compare/merge endpoints.
2. Add branch create/delete/move endpoints.
3. Add fork and merge request endpoints.
4. Enforce protected-branch rules and compare-and-swap tip updates.
5. Write audit entries for repository mutations.

Important:
- keep `src/api/v1/index.php` from growing even more chaotic by moving logic into shared libs
- return conflict payloads rich enough for future WASM and PHP compare views
- keep permissions conservative for private planning content

Update checklists after completion.
