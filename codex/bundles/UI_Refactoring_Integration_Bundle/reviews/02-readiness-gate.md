# Readiness Gate

Mark `READY` only when every critical and high item is checked or explicitly waived by the owner.

## Critical

- [ ] V2 denylist is clean on integration HEAD.
- [ ] Components full tests are green.
- [ ] BaseLib CSS exists in clean source checkout and regeneration is deterministic.
- [ ] Source-mode CanDoItAll build/browser proof passes.
- [ ] Container source-context build passes.
- [ ] No hidden local generated asset is required.

## High

- [ ] Components approval diffs are reviewed.
- [ ] FileTools dependency/package validator passes.
- [ ] All package families use selected `V`.
- [ ] CanDoItAll source pins reference exact final upstream commits.
- [ ] Legacy Material Icons contracts are removed.
- [ ] Representative large-desktop UI proof passes.
- [ ] Package-reference fallback mode passes from clean outputs.
- [ ] FileBrowser and FileInteraction host flows pass.
- [ ] Podman/macOS docs match current source mode.
- [ ] Development/main ancestry plan is prepared and v2 remains excluded.

## Medium / reporting

- [ ] All skips are explicit.
- [ ] Generated files were not hand-edited.
- [ ] No unrelated redesign is present.
- [ ] Execution report is complete.
- [ ] Remote write/publish authorization status is recorded.

**Decision:** NOT READY  
**Reviewer:** TBD  
**Date:** TBD  
**Blocking items:** TBD
