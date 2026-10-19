# Shared decision allocation boundary

**28/28 focused Unit cases pass again**, with no skips. The
[boundary refinement](../plan/P07-shared-decision-budget-boundary.md) is implemented.
The arithmetic now lives beside the policy vocabulary in SharedKernel, using
`DecisionBudgetId` and `DecisionBudgetAllocationId`. Engine decision identities,
source authority, journals, generation coordination and storage remain engine-owned.
No durable runtime integration or policy activation is claimed.

The three moved files match the signed `bc6dbdbfa` source after only reviewed type,
identity, namespace and exception-label renaming. That exact comparison is retained in
`production-bridge.json`. No calculation changed. Source search found no other consumers
of the unreleased names, stored budget rows or external API contracts. No compatibility
aliases, duplicated implementation, project references or DI registrations were added.

## Dependency and behavior proof

CodeAnalytics before `snap-20261019170735-90aa3b47`, after
`snap-20261019171037-90aa3b47`: five projects, 1,408 types, 9,395 members, 377 findings,
189 diagnostics and zero cycles, unchanged. Inventory and dependency queries confirm:

- SharedKernel has no project references.
- Process Contracts references SharedKernel; Runtime references Contracts.
- Agent Models references SharedKernel; Workflow Abstractions references Agent Models.
- No engine-to-engine reference was introduced.

The only finding replacement is the same Info `COMPLEXITY-002` for the 12-member balance,
now in SharedKernel. Its immutable balance/validation responsibility remains cohesive.
Existing workspace-load blocking diagnostics remain; actual builds pass. The architecture
gate passes for this boundary correction, with durable-owner and runtime gates still open.

Built SharedKernel, Runtime and Unit in standard Release, serialized `/m:1`, with local
sibling libraries. Existing compiler/NuGet warnings remain. Discovered exactly 28 cases
for `FullyQualifiedName~CanDoItAll.Tests.Unit.DecisionBudgetTests`, then executed that
filter with `--no-build --no-restore`. All pass; the command took 2.878 seconds. This
repeats the same arithmetic coverage rather than adding 28 different behaviors.

The full portability scan passes enforcement with 15,615 unchanged reviewed findings,
without writing the baseline. Documentation, rendering register and diff checks pass.
Stale bundle wording was also corrected to identify the later external-Agent and native
recovery evidence without claiming deleted-Agent entry or production-host restart.

## Evidence and resources

Owned candidate: `.artifacts/concurrent-mutations/validation/c4-shared-decision-budget`.
The 9,422 source and 1,784 runtime inputs were frozen and verified before release.
Source SHA-256: `608902B45A0B229E32EF901299E918B71E4C6041DFCD5A438334291602ED1492`.
Runtime SHA-256: `1083BA33EA1C15651E4BED2797738FFA341E4968D51FD340641242C1B8C0B339`.
Compact proof under ignored `proof/2026-10-19-planning-authority/c4-shared-decision-budget.json`:
`09010536A0C254BFE26FB34B1A79A4B8F1728BF62144BBF840766A889A24B61D`.

Ownership preceded creation. The entire execution command was observed, with zero
remaining observed processes and zero database delta. Four unattributed TEMP entries
were preserved. The pure tests created no database fixture or application host. The
existing isolated server remains for integration work; earlier cleanup blockers were
not retried. SPA remains on `codex/concurrent-mutations`, without merge or push.
