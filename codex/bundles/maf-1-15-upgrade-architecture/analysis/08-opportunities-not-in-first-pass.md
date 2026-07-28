# 1.15 Opportunities to Evaluate After Compatibility

These are valuable candidates, but none should be folded into the initial package/compatibility migration.

## Approval-Not-Required Bypassing

Potential benefit:

- expose only true human approvals;
- reduce approval UI noise;
- resume ordinary tool calls automatically.

Prerequisites:

- per-request decision API;
- stable session persistence;
- mixed-call tests;
- application mutation classification remains enforced;
- telemetry for auto-resumed calls.

Recommended timing: immediately after SB03 parity/security closure, under an explicit feature flag.

## Stable ToolApprovalAgent

Potential benefit:

- standardize “do not ask again” and auto-approval rules;
- remove some custom presentation/state logic.

Limits:

- MAF rules do not replace workspace scope, process governance, mutation classification, or external-target authorization;
- rule context/signatures changed;
- application policy must remain outer defense-in-depth.

Recommended timing: separate architecture bundle after 1.15 closure.

## Message Injection

Now-stable message injection could support:

- injecting recoverable policy guidance into a function loop;
- adding tool-produced context without ending the turn;
- manager/recovery messages during a run.

It may replace some prompt concatenation or transient-context hacks, but only after mapping persistence and provider behavior.

Recommended timing: separate experiment with per-service-call persistence enabled where required.

## FileMemoryProvider

Potential use:

- ephemeral agent scratch memory;
- isolated coding/research harness;
- local file-backed working notes.

Do not use as a replacement for CanDoItAll's cognitive memory, provider abstraction, durable project state, or governed artifacts.

## HarnessAgent

Potential use:

- isolated autonomous coding/research mode;
- standardized todo/mode/skills/file-memory composition;
- maximum iteration and compaction controls.

Do not convert ordinary CanDoItAll agents in this upgrade. Harness brings a different tool and context model.

## Workflow Session Fix

If SB05 proves that custom compatibility code exists solely for assembly-qualified external request payloads, simplify or remove it after a 1.13 checkpoint resumes under 1.15.

## Native Workflow Message Merge

If custom sorting, message reconstruction, or handoff result selection is found, replace only the portions for which 1.15 behavior is proven equivalent on the full runtime path.

## OpenAI Responses Hosting

1.15 exposes helpers for hosting an Agent Framework agent/workflow behind an OpenAI Responses-compatible API.

This is not the same as using the OpenAI Responses API as a provider. It could be useful later for:

- interoperable hosted CanDoItAll agents;
- external clients using a standard Responses contract;
- hosted workflow/session state.

It does not directly replace current background-response polling without a hosting redesign.

## Declarative `autoSend`

Only relevant if declarative workflows are already used or deliberately adopted. If used, 1.15 should eliminate duplicate completion content and unstable IDs. Keep this out of the core migration unless discovery finds an active path.

## AG-UI Split

The package split can enable a cleaner client/server boundary if AG-UI is later adopted. Current custom Blazor activity streams should not be replaced incidentally.

## Compaction

The summary-deserialization fix strengthens long sessions if compaction is active. The current architecture should first inventory history growth, custom memory, provider-managed history, and attachment handling before introducing compaction.

## LocalCodeAct and Shell Fixes

Useful only for paths that actually use MAF Harness/CodeAct shell tools. Custom command execution remains a separate security boundary.
