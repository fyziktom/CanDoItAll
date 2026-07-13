# SB01 Semantic Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB01-INV-01 | Every later extraction has a current owner and target owner. | `inventories/01-scope-inventory.md` |
| SB01-INV-02 | Baseline CodeAnalytics evidence is recorded before implementation. | `proof/SB01/manifest.md` |
| SB01-INV-03 | SB01 does not move production behavior. | Git diff/source review transcript |

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| Baseline responsibility map | SB01 | Implementation subbundles | Created before edits and updated if stale | Later subbundles fail entry gate if missing. |
