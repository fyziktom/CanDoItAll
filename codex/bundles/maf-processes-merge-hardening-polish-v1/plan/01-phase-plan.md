# Phase Plan

## Phase Sequence

1. Clean tracked transient work-package artifacts and ignore rules.
2. Rename test methods and add future leak guardrails.
3. Move software-delivery proof logic behind a domain driver or explicit domain adapter seam.
4. Reinforce Process Core, driver package, MAF, and gateway boundary tests.
5. Run merge validation and record closure evidence.

## Subbundle Dependency Map

```mermaid
gantt
title MAF Processes merge-hardening polish dependency map
dateFormat  YYYY-MM-DD
section Repo hygiene
SB01 artifact hygiene           :active, sb01, 2026-06-12, 1d
SB02 naming guardrails          :after sb01, sb02, 1d
section Domain ownership
SB03 software delivery driver   :after sb02, sb03, 2d
SB04 driver boundary hardening  :after sb03, sb04, 1d
section Closure
SB05 merge validation           :after sb04, sb05, 1d
```

## Critical Subbundles

- SB01 is critical because all later scans and tests assume transient work-package artifacts are gone from tracked content.
- SB02 is critical because the user explicitly requested bundle naming leak cleanup before merge.
- SB03 is critical because it addresses the architectural question: domain-specific software-delivery proof logic is still in the generic process dispatcher.
- SB05 is critical because it proves the working multi-team app delivery process behavior survived the polishing pass.

## Phase Gates

- Gate after SB01: tracked artifact scan must return no forbidden paths.
- Gate after SB02: forbidden naming scan must return no active test method leaks.
- Gate after SB03: process-focused tests must pass and source scan must show no stack-specific proof terms in generic dispatcher partials outside allowed domain adapter files.
- Gate after SB04: MAF/Core/Driver/Gateway boundary tests must pass.
- Gate after SB05: build, unit, integration, and smoke evidence are recorded.

## Suggested validation commands

Use PowerShell or Bash equivalents as appropriate:

```bash
git status --short
git ls-files | rg '(^01-execution-report\.md$|^codex/(bundles|bundle-exports)/|^codex/.*\.zip$)'
rg -n 'SB[0-9]{{2,3}}(_|-)?INV|SB[0-9]{{2,3}}|subbundle|bundle-exports|maf-processes-provider-hardening-followup|process-runtime-live-openai-verification-host-alpha' tests src Templates docs README.md --glob '!codex/skills/**'
rg -n '(Blazor|Razor|dotnet|\.csproj|\.slnx|npm|pnpm|yarn|vite|react|vue|svelte|javascript|typescript)' src/CanDoItAll.Modules.Processes/Automation/Dispatch --glob '!**/SoftwareDelivery*Adapter*.cs' --glob '!**/Domain/SoftwareDelivery/**'
dotnet test tests/CanDoItAll.Tests.Unit --filter "Process|Driver|AgentRuntimeHardeningStaticRegression|SecretScanning|Repository"
dotnet test tests/CanDoItAll.Tests.Integration --filter Process
dotnet build CanDoItAll.slnx --no-restore
```

The first two `rg` scans are expected to return no matches after the relevant subbundle. If `rg` is unavailable, use `git grep` or PowerShell `Select-String` over `git ls-files` output.
