# SB05 Semantic Invariants

## Invariant MAF-SB05-DEVELOPMENT-SKILL-SCOPE

- Invariant ID: `MAF-SB05-DEVELOPMENT-SKILL-SCOPE`
- Source raw note: software-development image analysis can exist, but it must have its own development-owned capability and process-scoped activation.
- Expected behavior: `development-image-analysis-guidance-inline-skill` carries the development guidance, the screenshot writeback storage step requires it, and the applicability step denies the development capability/tag.
- Disallowed shallow implementation: keeping development or UI screenshot analysis text in common workspace image tool prompts.
- Failing-first test: `bundle://proof/SB05/transcripts/adversarial-negative.txt` proves the development capability key is absent from common MAF and common workspace tool templates.
- Passing test: `Dotnet_ui_screenshot_template_scopes_development_image_guidance_to_storage_step` in `bundle://proof/SB05/transcripts/passing.txt`.
- Changed source files: `repo://Templates/Capabilities/skills/instructions/development-image-analysis.md` with hash `56F4DF48D1E77D8F484E82C53850DA4DF7953D973E8600AA3A4A35355B616FD6`.
- Production assertions: capability seed materialization includes the development image skill and the process template scopes it by step.
- Red-team negative case: a management/applicability step must not receive development image-analysis guidance in its capability scope.
- Downstream dependency check: SB04 handoff carries the template scope into runtime metadata and SB01 keeps common defaults neutral.
