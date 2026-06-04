# SB05 Semantic Invariants

- Invariant ID: `SB05-INVARIANT-001`
- Source raw note: `RQ-006` Image-generation internal tool attachment must move out of MAF into the runtime-provider seam without tool availability or approval-policy drift.
- Expected behavior: `ImageGenerationAgentRuntimeToolProvider` registers from AgentFramework module ownership; eligible agents still receive exactly `image_generation_create`; disabled agents receive no image tool; project-asset source reads still require project-structure read access; MAF has no image-generation-specific attach code.
- Disallowed shallow implementation: Moving the file while leaving `AttachInternalImageGenerationToolsAsync` in MAF, renaming the tool, bypassing `AgentImageGenerationAccessMetadata`, dropping project-asset source access checks, or weakening runtime-provider approval wrapping.
- Failing-first test: `bundle://proof/SB05/transcripts/failing-first-maf-helper-dependency-build.txt` records the first post-move build failure because copied code referenced MAF-local helpers; localizing those helpers proves the provider no longer reaches back into MAF for image behavior.
- Passing test: `bundle://proof/SB05/transcripts/image-generation-unit-tests.txt`, `bundle://proof/SB05/transcripts/image-generation-runtime-integration-smoke.txt`, and `bundle://proof/SB05/transcripts/solution-build.txt`.
- Changed source files: `bundle://proof/SB05/source-assertions/changed-file-hashes.txt`.
- Production assertions: `bundle://proof/SB05/source-assertions/image-generation-provider-source-assertions.txt`.
- Red-team negative case: The MAF attach-name scan would fail if old image attach helpers remained; the unit tests would fail if enabled/disabled image access stopped controlling tool exposure; the runtime smoke would fail if MAF no longer attached the provider tool to eligible agents.
- Downstream dependency check: SB06 may start with both project-structure and image-generation product tool providers migrated out of MAF; MAF still owns provider-native model/client composition for core runtime execution, which is outside SB05 scope.
