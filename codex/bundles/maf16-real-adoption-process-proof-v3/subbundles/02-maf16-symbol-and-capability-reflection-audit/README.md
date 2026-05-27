# SB02: 02-maf16-symbol-and-capability-reflection-audit

## Goal

Prove MAF 1.6 symbol availability by compile/reflection tests.

## Required work

- Create a focused test or tool that inspects loaded MAF assemblies for IChatMessageInjector, MessageAIContextProvider, AgentSessionFiles, SkillFrontmatter, OpenTelemetryChatClient, workflow expected output types, A2A v1 types.
- Do not rely only on rg/source grep.
- Record exact assembly versions and symbol availability.
- Update the adoption matrix from reflection results.

## Required proof

- Failing-first or adversarial proof.
- Passing production-path proof.
- Source assertions with exact repo paths.
- Anti-stub audit.
- Changed-file hashes.
- Classification: MAF package-level / MAF adapter-level / process runtime-level / template/UI-level.
- Note whether this subbundle changes behavior or only improves proof/documentation.

## Closure criteria

This subbundle is complete only when proof files under `proof/SB02` are updated and downstream subbundles can rely on it.

## Status

- Completed

## Objective

Prove the loaded MAF 1.6 assemblies expose the expected runtime symbols.

## Covered Inputs

- RQ02 compile and reflection proof.

## Prerequisites

- MAF package references remain pinned to the intended 1.6 packages.

## Exact Source References

- `repo://tests/CanDoItAll.Tests.Unit/Maf16CapabilityReflectionTests.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/CanDoItAll.AgentFramework.Maf.csproj`

## Deliverables

- Runtime reflection test and proof manifest.

## Dependency Impact

- SB03, SB04, and SB18 use this as the package capability boundary.

## Validation Depth

- Compile-time references and runtime assembly reflection.

## Implementation Steps

- Add the reflection test.
- Run the focused unit test.
- Record proof in `proof/SB02`.

## Do Not Do

- Do not claim adoption from NuGet references alone.

## Acceptance Checklist

- Reflection test passes against loaded assemblies.

## Proof Required

- `proof/SB02/manifest.md` and `proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- No browser route is affected.

## Progression Gate

- Reflection proof must pass before final MAF adoption claims.

## Suggested Agent Prompt

Validate the actual loaded MAF assemblies and reject source-only symbol claims.
