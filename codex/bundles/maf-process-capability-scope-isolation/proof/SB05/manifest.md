# SB05 Proof Manifest

## Subbundle

- Subbundle: `SB05`
- Status: `Completed`
- Owned requirement: development-specific image analysis guidance must live in a development capability and be scoped by processes, not common workspace tools.
- Test name: `Dotnet_ui_screenshot_template_scopes_development_image_guidance_to_storage_step`

## Changed Files And Hashes

| File | SHA-256 |
|---|---:|
| `repo://Templates/Capabilities/skills/instructions/development-image-analysis.md` | `56F4DF48D1E77D8F484E82C53850DA4DF7953D973E8600AA3A4A35355B616FD6` |

## Proof Artifacts

- Semantic invariant contract: `bundle://proof/SB05/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB05/transcripts/adversarial-negative.txt`
- Passing transcript: `bundle://proof/SB05/transcripts/passing.txt`
- Anti-stub audit transcript: `bundle://proof/SB05/transcripts/anti-stub.txt`
- Source assertion: `repo://Templates/Capabilities/skills/instructions/development-image-analysis.md`
- Source assertion: `repo://Templates/Capabilities/skills.json`
- Source assertion: `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`

## Closure

- Failing-first: `bundle://proof/SB05/transcripts/adversarial-negative.txt` records that the development image capability does not live in common MAF or common workspace tool templates.
- Semantic positive proof: `bundle://proof/SB05/transcripts/passing.txt` records capability seed and process scope tests.
- Anti-stub audit: `bundle://proof/SB05/transcripts/anti-stub.txt` records no placeholder implementation in the development image scope assets.
