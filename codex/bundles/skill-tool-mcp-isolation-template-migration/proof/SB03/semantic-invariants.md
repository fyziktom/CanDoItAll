# SB03 Semantic Invariants

## SB03_INV_FILE_001

- Source raw note: file skills must load from typed descriptors and validate Codex-compatible `SKILL.md` metadata.
- Expected behavior: a file skill under the workspace loads `name`, `description`, and instructions from `SKILL.md`, preserves script policy metadata, and exposes a shared capability descriptor.
- Disallowed shallow implementation: accept any directory as a skill root or skip `name`/`description` validation.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Loading/FileSkillLoader.cs`, `repo://src/CanDoItAll.AgentFramework.Skills/Loading/SkillMarkdownParser.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_FILE_003` proves a directory without `SKILL.md` does not load.
- Downstream dependency check: SB08 can adapt loaded file skills to MAF without reimplementing path or metadata validation.

## SB03_INV_FILE_002

- Source raw note: external skill roots must remain explicitly allowed.
- Expected behavior: a skill root outside the workspace fails with `CommandPolicy`, `FileSkill` transport, capability key, and repair hint when `allowedExternalRoots` does not cover it.
- Disallowed shallow implementation: silently ignore the missing allowlist or fall back to workspace-relative interpretation.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Loading/FileSkillLoader.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_FILE_002` creates a valid external `SKILL.md` but omits the allowlist and expects failure before file activation.
- Downstream dependency check: SB06/SB10 can validate external-root setup with the same loader diagnostic instead of UI-only checks.

## SB03_INV_FILE_003

- Source raw note: missing skill files must fail predictably and must not be hidden behind fallback instructions.
- Expected behavior: a configured directory without `SKILL.md` returns `TemplateValidation` with field path `$.skillRoot` and repair hint.
- Disallowed shallow implementation: return success with empty instructions or skip missing file errors.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Loading/FileSkillLoader.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_FILE_003` creates the directory only and asserts the exact diagnostic category and field path.
- Downstream dependency check: seed/template loading can block bad file skills before MAF runtime composition.

## SB03_INV_INLINE_001

- Source raw note: inline skills must preserve instructions and resources from typed templates.
- Expected behavior: inline skill loading validates name, description, instructions, preserves resources, and exposes tags/operation classifications to policy.
- Disallowed shallow implementation: collapse inline resources into one instruction string or drop operation metadata.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Loading/InlineSkillLoader.cs`, `repo://src/CanDoItAll.AgentFramework.Skills/Descriptors/SkillDescriptorFactory.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_INLINE_002` rejects an inline resource with empty content.
- Downstream dependency check: SB06 can materialize inline skill templates without losing resource data.

## SB03_INV_INLINE_002

- Source raw note: invalid inline resources must produce structured validation diagnostics.
- Expected behavior: empty inline resource content fails with `TemplateValidation` and field path `$.inlineSkill.resources[0].content`.
- Disallowed shallow implementation: drop invalid resources silently or allow empty context entries.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Loading/InlineSkillLoader.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_INLINE_002` supplies a named resource with whitespace content.
- Downstream dependency check: SB10 setup/save can reuse the loader diagnostic for inline skill resource validation.

## SB03_INV_REGISTERED_001

- Source raw note: registered skills must bind through explicit descriptors instead of arbitrary reflection at call sites.
- Expected behavior: `RegisteredSkillResolver` resolves a typed `ImplementationKey` through `RegisteredSkillRegistry` and returns a loaded skill without `Type.GetType` or DI reflection in the skills implementation.
- Disallowed shallow implementation: resolve registered services from arbitrary service type strings at runtime.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Registered/RegisteredSkillRegistry.cs`, `repo://src/CanDoItAll.AgentFramework.Skills/Registered/RegisteredSkillResolver.cs`, `bundle://proof/SB03/transcripts/static-performance-scan.txt`
- Red-team negative case: `SB03_INV_REGISTERED_002` proves a missing key fails explicitly.
- Downstream dependency check: SB08 MAF adapter can map old registered service-type config into key bindings at the adapter boundary instead of keeping reflection in loader code.

## SB03_INV_REGISTERED_002

- Source raw note: missing registered skill bindings must fail with actionable diagnostics.
- Expected behavior: an unregistered implementation key returns `ImplementationMissing` with the key and repair hint.
- Disallowed shallow implementation: skip missing registered skills or create placeholder instructions.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Registered/RegisteredSkillResolver.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_REGISTERED_002` resolves `skills.missing` against an empty registry.
- Downstream dependency check: SB10 setup/API can surface missing registered bindings without guessing service type names.

## SB03_INV_REGISTERED_003

- Source raw note: retired registered skills must not silently participate in runtime skill composition.
- Expected behavior: a retired registered descriptor returns `CapabilityUnavailable` and a retired diagnostic.
- Disallowed shallow implementation: ignore retirement state or load retired sandbox skill services.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Registered/RegisteredSkillResolver.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_REGISTERED_003` uses retired `workspace-delivery-skill` and expects failure before registry lookup.
- Downstream dependency check: SB08 can replace current MAF retired-skill branches with descriptor availability state.

## SB03_INV_POLICY_001

- Source raw note: file, inline, and registered skills must use the shared typed access policy/effective-set model.
- Expected behavior: skill descriptors map into `CapabilityExposureDescriptor` and are denied by operation classification, tag, and registered implementation key through the SB01 evaluator.
- Disallowed shallow implementation: keep process-step skill exclusion as loader-specific behavior.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://src/CanDoItAll.AgentFramework.Skills/Descriptors/SkillExposureDescriptorFactory.cs`, `bundle://proof/SB03/transcripts/source-assertions.txt`
- Red-team negative case: `SB03_INV_POLICY_001` denies file, inline, and registered skills through three different selector kinds and expects no allowed capabilities.
- Downstream dependency check: SB08/SB11 can use the same evaluator for skill restrictions across MAF, processes, workflows, and UI preview.

## SB03_INV_SEED_001

- Source raw note: current seeded inline skills must survive the move to a typed loader.
- Expected behavior: every existing markdown file under `SeedAssets/instructions/skills` materializes as an inline descriptor and loads through `InlineSkillLoader`.
- Disallowed shallow implementation: validate only synthetic inline skills and ignore the current seeded instruction assets.
- Failing-first proof: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing proof: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Changed source files and hashes: `bundle://proof/SB03/changed-file-hashes.txt`
- Production assertions: `repo://tests/CanDoItAll.Tests.Unit/SkillLoaderContractsTests.cs`, `repo://src/CanDoItAll.AgentFramework.Skills/Loading/InlineSkillLoader.cs`
- Red-team negative case: the test aggregates per-asset load diagnostics and fails if any seeded markdown asset produces a loader failure.
- Downstream dependency check: SB06 can migrate seeded inline skills toward templates with confidence that the new loader accepts current assets.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `LoadedSkill` | `repo://src/CanDoItAll.AgentFramework.Skills.Abstractions/Skills.cs`, file/inline/registered loaders | `repo://tests/CanDoItAll.Tests.Unit/SkillLoaderContractsTests.cs` | `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt` | `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt` |
| `SkillLoadResult` | `repo://src/CanDoItAll.AgentFramework.Skills.Abstractions/Skills.cs` | `repo://tests/CanDoItAll.Tests.Unit/SkillLoaderContractsTests.cs` | `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt` | `SB03_INV_FILE_002`, `SB03_INV_FILE_003`, `SB03_INV_INLINE_002`, `SB03_INV_REGISTERED_002`, `SB03_INV_REGISTERED_003` |
| `CapabilityExposureDescriptor` | `repo://src/CanDoItAll.AgentFramework.Skills/Descriptors/SkillExposureDescriptorFactory.cs` | `repo://tests/CanDoItAll.Tests.Unit/SkillLoaderContractsTests.cs` | `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt` | `SB03_INV_POLICY_001` |
| `CapabilityDiagnostic` | `repo://src/CanDoItAll.AgentFramework.Skills/Diagnostics/SkillDiagnostics.cs` | `repo://tests/CanDoItAll.Tests.Unit/SkillLoaderContractsTests.cs` | `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`, `bundle://proof/SB03/transcripts/static-performance-scan.txt` | `SB03_INV_FILE_002`, `SB03_INV_FILE_003`, `SB03_INV_INLINE_002`, `SB03_INV_REGISTERED_002`, `SB03_INV_REGISTERED_003` |

## Anti-Stub Audit

- `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
