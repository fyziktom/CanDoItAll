# Structured Input

## Mission
Prepare the next implementation-ready bundle for `maf-processes-refactor`.

## Direction
Do not start Process Core extraction yet. Continue module-local, behavior-preserving isolation inside `CanDoItAll.Modules.Processes/Automation/Dispatch`.

## Primary Seam
Extract candidate construction / candidate factory / cooperation metadata boundary from `LoadDispatchCandidateAsync` and `ProcessRunAutomationDispatchService.Cooperation.cs`.

## Non-Goals
- No `CanDoItAll.Processes.Core`.
- No production `IProcessDriverPack`, process driver registry, or driver packages.
- No UI or responsive/mobile validation.
- No EF entity moves.
- No public tool name or process runtime behavior changes.

## Proof Policy
Runtime/service refactor only. Browser validation should be `N/A`. If UI proof unexpectedly becomes necessary, use only large desktop/PC proof; do not run small, medium, mobile, phone, tablet, Android, iPhone, or responsive proof.
