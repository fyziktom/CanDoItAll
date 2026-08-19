# SB13 proof manifest

Final candidate commit: `dea90cfd4cc77e60f1a7d07a2dc16d44165840f9`

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | Final-gate preflight and blocker semantics. |
| `transcripts/01-final-candidate-preflight.md` | Clean-head and exact package-mode prerequisite evidence. |
| `transcripts/02-package-feed-blocker.md` | Official-feed, sibling-source, and publication-path evidence. |
| `transcripts/03-static-final-guards.md` | Exact-head static validators and checksum verification. |

The one restore, solution build, stable filtered solution test, and hosted CI matrix run were not spent.
The package required by all package-mode lanes is absent from the repository's only configured feed, so
each expensive command is known to fail before it can validate the candidate. FINAL is Not Ready.
