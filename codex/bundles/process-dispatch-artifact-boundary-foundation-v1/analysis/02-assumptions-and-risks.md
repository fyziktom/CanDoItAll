# Assumptions And Risks

## Assumptions

- The previous execution snapshot boundary is the stable entry point for this bundle.
- The current artifact/projection behavior is relied on by live process automation and must not be simplified.
- The first artifact boundary should stay inside the Processes module, not a new Core project.
- Storage placement and DB artifact recording may remain in dispatcher/application services until pure planning seams are proven.

## Critical Path Risks

- Moving projection code too aggressively may break current-run evidence or duplicate/stale artifact suppression.
- Extracting validation rules without a complete inventory may silently drop special cases such as browser proof, zero-test proof, warning-free build proof, provider-native browser artifacts, project-structure weakening detection, or process mock artifacts.
- Count-only tests can miss trust-status, lineage, sensitivity, expectation, and external-reference changes.
- Introducing a Core or driver project too early would freeze current dispatcher assumptions into the wrong layer.

## Validation Risks

- Artifact integration tests may be slow; split focused unit tests from process-filtered integration smoke.
- Projection behavior may depend on workspace paths and storage catalogs; test with controlled path fixtures.
- Lineage strings and JSON payloads can drift; assert structured fields and key tokens, not fragile full prose unless current tests already do so.

## Reopen Triggers

- Any process artifact expectation that was previously satisfied becomes unsatisfied after migration.
- Any required artifact can be marked satisfied without current-run evidence.
- Any lineage/source external-reference key loses run/attempt/source identity.
- Any new helper calls AgentFramework runtime services directly.
- Any small/medium/mobile proof artifact appears.
