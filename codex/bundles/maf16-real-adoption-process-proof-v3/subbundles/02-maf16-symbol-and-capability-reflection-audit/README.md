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
