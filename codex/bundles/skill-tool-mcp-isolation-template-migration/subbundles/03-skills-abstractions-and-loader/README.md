# 03 Skills Abstractions And Loader

## Status

- `Completed`

## Objective

- Build the dedicated skill abstraction and loader layer for file, inline, and registered skills, including Codex-compatible `SKILL.md` validation and resource/script policy handling.

## Success Criteria

- File skills load from typed descriptors and validate `SKILL.md`.
- Inline skills load instructions/resources from typed templates.
- Registered skills bind through explicit descriptors instead of arbitrary stringly reflection at call sites.
- Every skill exposes the common capability exposure descriptor required by the access policy evaluator, including typed tags, source identity, registered key where applicable, and operation classifications if the skill enables execution behavior.
- Skill load failures report source type, path or registered key, capability key, failure category, and repair hint without fallback instructions.

## Covered Inputs

- R01, R02, R03, R07, R08, R09, R10, R12, R13, R14, R15.

## Prerequisites

- SB01 contracts and naming validation pass.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Skills.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/Seeds/SandboxWorkspaceSeedBuilder.cs`
- `repo://src/CanDoItAll.AgentFramework.Persistence/SeedAssets/instructions/skills`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`

## Deliverables

- Skill abstraction and implementation projects or folders agreed in SB01.
- File skill loader with `SKILL.md` front matter/body validation.
- Inline skill loader with resource support.
- Registered skill descriptor resolver that can be tested without arbitrary reflection at runtime.
- Skill exposure descriptor factory for file, inline, and registered skills.
- Script execution policy and trust-level validation moved out of MAF.
- Structured loader diagnostics for missing file, invalid metadata, external root rejection, oversized context, registered binding failure, and script policy rejection.

## Dependency Impact

- SB05 hardens skill loading before template/runtime consumption.
- SB06 uses skill descriptors for template-backed seed materialization.
- SB08 uses skill services to replace MAF skill builder logic.
- SB10 uses the same validation messages in setup UI.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Map current file, inline, and registered skill configuration fields to SB01 contracts.
2. Implement `SKILL.md` validator with required `name` and `description`.
3. Implement inline skill loader and resource validation.
4. Implement registered skill descriptor validation and DI lookup service.
5. Implement skill exposure descriptor factory and tests for key, tags, source identity, registered binding, and policy selector matching.
6. Add diagnostic mapping for missing files, invalid metadata, external root allowlists, retired registered skills, and script policy.
7. Add unit tests for missing files, invalid metadata, external root allowlists, retired registered skills, oversized descriptions, script policy, and exposure descriptor access-policy participation.
8. Add integration tests proving existing seeded skills load through the new layer.

## Scope Exceptions

- Do not migrate seed builder to templates in this subbundle.
- Do not reconnect MAF skill attachment yet.

## Do Not Do

- Do not hide missing skill files behind fallback instructions.
- Do not execute skill scripts during load validation.
- Do not generate XML documentation comments.
- Do not use arbitrary runtime reflection at call sites for registered skills.
- Do not keep process-step skill exclusion as skill-loader-specific behavior; expose metadata and let the shared evaluator decide.

## Acceptance Checklist

- Existing file skills load from configured roots.
- Inline skill resources are preserved.
- Missing registered skill types fail with actionable messages.
- External root usage remains explicitly allowed and test-covered.
- Missing/invalid skill diagnostics include capability key, source path or registered key, category, and repair hint.
- Skill exposure descriptors can be denied by key, tag, kind, and operation classification through the shared policy evaluator.

## Proof Required

- Skill loader unit tests.
- Existing seeded skill integration tests through the new loader.
- Access-policy participation tests for file, inline, and registered skill descriptors.
- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`

## Execution Proof

- Manifest: `bundle://proof/SB03/manifest.md`
- Semantic invariants: `bundle://proof/SB03/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB03/transcripts/failing-first-skill-loader-contracts.txt`
- Passing targeted tests: `bundle://proof/SB03/transcripts/passing-skill-loader-contracts.txt`
- Full build: `bundle://proof/SB03/transcripts/dotnet-build-solution.txt`
- Source assertions: `bundle://proof/SB03/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/anti-stub-audit.txt`
- Static/performance scan: `bundle://proof/SB03/transcripts/static-performance-scan.txt`
- Changed file hashes: `bundle://proof/SB03/changed-file-hashes.txt`

## Browser Validation Logging

- N/A for loader work. UI proof is SB10.

## Progression Gate

- Result: `Passed`
- SB03 proved file `SKILL.md` validation, explicit external-root policy, inline resource preservation, registered-key binding without loader reflection, retired/missing registered diagnostics, seeded inline asset parity, and shared access-policy participation.

## Suggested Agent Prompt

```text
Implement subbundle SB03 only. Build the skill loader layer, exposure descriptors, and tests. Preserve current file, inline, registered, external-root, and script-policy behavior. Do not reconnect MAF yet.
```

