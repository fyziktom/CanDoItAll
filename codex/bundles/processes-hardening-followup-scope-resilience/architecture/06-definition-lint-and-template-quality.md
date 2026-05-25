# Process Definition Lint And Template Quality

## Why This Matters

The runtime can enforce boundaries, but many failures originate in bad process definitions:

- architecture step also says implement,
- review step has no repair branch,
- workflow-backed role has no artifact mapping,
- required artifact has vague format,
- downstream step requires upstream artifact that source step never declares,
- step role has mutating tools when it should be read-only.

## ProcessDefinitionLinter

Add lint rules that can run:

- before publishing a process definition,
- before starting a process run,
- during bundle/seed template validation,
- in tests.

## Required Lint Rules

| Rule ID | Rule |
| --- | --- |
| PDL001 | Non-mutating step must not require product mutation tools unless explicitly marked. |
| PDL002 | Architecture/scope/planning step must not have deliverable/product source artifact expectations unless explicitly marked as artifact destination. |
| PDL003 | Implementation step must have a clear mutation target or artifact destination. |
| PDL004 | Review/QA step that can reject output should have a repair/rework/no-go branch or explicit block policy. |
| PDL005 | Workflow-backed role step must define artifact mapping from workflow output to process artifact expectations. |
| PDL006 | Subprocess parent step required artifacts must map to child artifacts, not generic child completion. |
| PDL007 | Required artifact inputs must reference source step expectations that exist and are required/produced. |
| PDL008 | Artifact validation summaries must not be the only source of machine mode/format when strict validation is enabled. |
| PDL009 | Step with browser/runtime proof must have launch environment or upstream runnable target. |
| PDL010 | No step may have both `artifact-only` and `mutate-product` semantics unless it is explicitly a recovery/repair step. |

## Simulation

Add a dry-run process simulation that prints:

- each step's boundary,
- allowed/denied operations,
- required artifacts,
- required inputs,
- possible branch transitions,
- missing unblock paths,
- expected tool families.

This gives process authors feedback before agents run.
