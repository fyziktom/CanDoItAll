# Proof artifact map

For each subbundle create under the repository bundle execution area:

- `proof/SBxx/manifest.json`
- `proof/SBxx/semantic-invariants.md`
- `proof/SBxx/changed-files-and-ranges.json`
- `proof/SBxx/impacted-tests-request.json`
- `proof/SBxx/impacted-tests-response.json`
- `proof/SBxx/test-execution.json`
- `proof/SBxx/build-execution.json`
- `proof/SBxx/source-guard.txt`
- `proof/SBxx/dependency-evidence.md` when architecture-relevant
- `proof/SBxx/ui-parity.md` when rendered UI changes
- screenshots/browser traces only at named checkpoints

Every path in a proof manifest must exist before closure. Do not place fake empty artifacts to satisfy file checks.
