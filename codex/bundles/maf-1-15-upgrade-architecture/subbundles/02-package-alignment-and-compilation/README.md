# SB02 — Package Alignment and Compilation

## Status

- `Ready after A1`

## Objective

Move all direct MAF references to the exact 1.15 release train with the smallest possible compile-only change set and preserve 1.13 mixed-tool approval behavior during the parity phase.

## Success Criteria

- Stable MAF packages resolve to `1.15.0`.
- MAF A2A packages resolve to `1.15.0-preview.260722.1`.
- Two shared MSBuild properties own these versions.
- No direct/transitive 1.13 MAF package remains.
- No unrelated dependency is downgraded or broadly updated.
- Current mixed-tool approval behavior is explicitly preserved.
- Target projects and baseline tests compile/run far enough to expose behavioral work for later subbundles.
- Compile changes are documented without suppressing new warnings blindly.

## Covered Requirements

- R02, R03, R04, R09, R15, R17, R21, R22

## Prerequisites

- A1 GO;
- fixture hashes frozen;
- package graph and warning baseline available.

## Exact Source References

- `Directory.Build.props`
- main MAF adapter project
- workflow MAF adapter project
- MAF hosting project
- every direct MAF package reference discovered in SB01
- every `ChatClientAgentOptions` construction discovered in SB01

## Deliverables

- shared stable/preview version properties;
- updated direct package references;
- package restore graph;
- compile migration notes;
- explicit parity option for mixed approvals;
- warning-delta report;
- `proof/SB02/package-alignment.md`;
- `proof/SB02/build/`;
- updated execution report.

## Implementation Steps

1. Add the two shared version properties.
2. Replace direct stable MAF literals with the stable property.
3. Replace direct A2A/Hosting.A2A literals with the preview property.
4. Restore and inspect the graph before changing adjacent dependencies.
5. Resolve compile breaks with API-compatible edits only.
6. Set `DisableApprovalNotRequiredFunctionBypassing = true` on relevant 1.15 agent options for parity.
7. Keep approval response binding enabled.
8. Inspect custom provider stacks; add explicit binding only if default middleware is bypassed.
9. Build targeted projects.
10. Run package alignment tests and baseline deterministic tests.
11. Temporarily inventory MAF warnings without blanket suppression.
12. Record unresolved behavioral failures for SB03-SB07.

## Do Not Do

- do not enable repository-wide Central Package Management;
- do not downgrade MEAI/OpenAI/Azure/MCP simply to mirror upstream;
- do not disable approval binding;
- do not adopt Harness, AG-UI, declarative workflows, FileMemory, or Responses hosting;
- do not remove session/approval/handoff workarounds;
- do not change finalizer semantics.

## Acceptance Checklist

- [ ] shared stable property
- [ ] shared preview property
- [ ] exact target versions resolved
- [ ] no old MAF package
- [ ] no downgrade warning
- [ ] parity mixed-approval option explicit
- [ ] approval binding not disabled
- [ ] target projects build
- [ ] warning delta classified
- [ ] execution report updated

## Proof Tier

- `Standard` for package edits
- `Behavioral` for parity option and effective middleware

## Proof Required

- Materialize every evidence path listed under `Deliverables`; do not leave proof only in chat or terminal scrollback.
- Record exact commands, exit codes, repository SHA, relevant environment details, and timestamps.
- Preserve failing-first evidence before the passing result whenever behavior changes.
- Hash persisted-state fixtures and redact secrets or sensitive payloads.
- Link the final proof from `reviews/01-execution-report.md`.

## Progression Gate

SB03 may start only after package alignment is deterministic and no hidden 1.13 assembly remains.

## Reopen Triggers

- package graph changes;
- a provider factory bypasses default middleware;
- adjacent dependency update changes MEAI merge behavior;
- A2A preview version mismatch appears.

## Suggested Agent Prompt

```text
Implement SB02 only. Align MAF stable and A2A preview packages through two shared MSBuild properties, preserve the 1.13 mixed-approval surface explicitly, keep approval binding enabled, resolve compile-only changes, capture package/build/warning proof, and do not adopt optional 1.15 features.
```
