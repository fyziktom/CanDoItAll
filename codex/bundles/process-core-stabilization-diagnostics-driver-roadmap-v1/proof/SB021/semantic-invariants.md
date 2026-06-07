# SB021 Semantic Invariants

## Raw Note Closure
- Raw note owned: continue toward a stable Process Core without moving production projection or validation orchestration.
- Literal closure: source order, lineage, satisfaction checks, browser/runtime evidence validation, and artifact content reads remain module-local.

## Shallow-Pass Trap
- A shallow pass would add descriptor records while still letting side-effecting dispatcher files reference Core directly or letting Core leak storage/workspace vocabulary.
- This gate requires adapter-only Core consumption, focused descriptor tests, full dispatch integration proof, API/boundary proof, build proof, and source scans.

## Semantic Positive Proof
- `ProcessCoreArtifactProjectionEligibilityRules_SB019_INV_001_describes_projection_sources_without_storage_paths` proves Core projection source descriptors classify runtime and record-only source facts without write paths.
- `ProcessCoreArtifactValidationRequirementDescriptorRules_SB020_INV_001_preserves_mode_and_policy_classification` proves Core validation descriptors preserve expectation modes and producer policy facts.
- `ProcessRunAutomationDispatchServiceTests` passed with 539 tests.

## Adversarial Negative Proof
- Evidence mode still rejects assistant-response producer satisfaction.
- Runtime proof still allows provider-native browser evidence.
- Optional narrative artifacts do not require stored content merely because a reference string exists.
- `bundle://proof/SB021/transcripts/core-descriptor-forbidden-token-scan.txt` proves no module, infrastructure, storage, workspace, file IO, driver, dispatcher, DbContext, or logger tokens leaked into the Core descriptor file.

## Anti-Stub Audit
- `bundle://proof/SB021/transcripts/anti-stub-audit.txt` found no TODO, NotImplemented, stub, or fixture-specific markers in changed descriptor production files.

## Boundary Proof
- `ProcessArtifactValidationDescriptorAdapter.cs` is the only new module bridge to the Core descriptor rules.
- No production process driver API was introduced.
- No UI, browser, mobile, or media files were changed.
