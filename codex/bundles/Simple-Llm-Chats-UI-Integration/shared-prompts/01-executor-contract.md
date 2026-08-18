# Executor Contract

Execute only the current subbundle.

1. Read root status, prerequisites, current subbundle, owned requirements, architecture records, and reopen triggers.
2. Record the actual start commit and stop on unreviewed drift that changes ownership or proof.
3. Use current SharedInfo skills and verify their hashes when the active install may be stale.
4. Inspect exact source and nearby tests before editing.
5. Implement the smallest coherent outcome; do not activate later phases early.
6. Use CodeAnalytics impacted-test selection from the actual final diff and reject zero discovery.
7. Record exact commands, discovery, results, screenshots/overlays when applicable, architecture result, and progression decision.
8. Commit bounded proof paths by maintaining an explicit `.gitignore` exception for this bundle.
9. Reopen prerequisites when later evidence invalidates them.
10. Stop at the current progression gate; do not run the next subbundle automatically when a user/manual gate is required.
