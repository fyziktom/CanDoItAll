# SB00 proof manifest

- product/proof head: `5522880cbf3101ed54c216ab74cac3b8ff2bade0`
- implementation commit from the original bundle: `16b6aa4b60dc88a6134dd6c9c9e634c064ac5847`
- synchronized development: `eb6be3ea38075b442d24976655f5c45ac08bd6b5`
- dependency mode: local sibling source projects
- host: Microsoft Windows 10.0.26200, x64; .NET SDK 10.0.303
- database: not used by the focused prior-failure slice

## Artifact inventory

| Artifact | Purpose |
|---|---|
| `semantic-invariants.md` | Semantic adequacy proof. |
| `prior-failures.slnx` | Durable three-project reproduction surface for the exact filter. |
| `transcripts/01-prior-stable-evidence.md` | Provenance of the original 8,121/19 stable result. |
| `transcripts/02-development-focused-19.md` | Synchronized development result: 11 pass, 8 fail. |
| `transcripts/03-feature-focused-19.md` | Synchronized feature result: 12 pass, 7 fail. |
| `transcripts/04-environment-deviations.md` | Transparent diagnostic and host-lock record. |
| `transcripts/05-codeanalytics-snapshot.md` | Post-merge architecture snapshot. |
| `transcripts/06-cp0-validator-results.md` | Passing bundle, traceability, test-policy, and architecture gates. |
| `../../inventories/03-prior-failure-classification-template.md` | Concrete 19-case classification. |

SB00 changes no production source. The merge introduced documentation only; the focused comparison
finds zero BranchInduced and zero Unresolved cases. CP0 may unlock SB01 after the bundle validators pass.
