# Scope Inventory

## In Scope

| Area | In scope work |
| --- | --- |
| MAF workspace plugin | Remove common software/UI-specific prompt defaults and wrappers from image analysis. |
| MAF capability access | Add typed scoped runtime policy input, pass required capabilities, and preserve suppression diagnostics. |
| Runtime tool providers | Add stable provider identity to provider-generated capability descriptors if provider-level suppression is exposed. |
| Process template schema | Add validated per-step capability scope and scoped instruction fragments. |
| Process assignment runtime | Persist effective step scope and use it during dispatch. |
| Process-to-MAF adapter | Translate process-neutral scope into MAF metadata and runtime policy. |
| Development image analysis | Move UI screenshot analysis instructions into a dedicated development owner. |
| Tests | Add unit and integration proof for generic prompts, suppression, required capabilities, metadata, and end-to-end management-only step behavior. |

## Out Of Scope For Preparation

| Area | Reason |
| --- | --- |
| Production code implementation | User asked to prepare bundle only. |
| Database migration execution | Deferred to execution subbundle after contract design. |
| UI changes | Not required unless process authoring UI must expose the new schema in a later bundle. |
| Removing software-delivery processes | They are valid domain owners; the leak is common MAF ownership. |

## Candidate Domain-Leak Terms To Scan During Execution

- `software-delivery`
- `software UI`
- `UI state`
- `UI design`
- `screenshot comparison`
- `browser proof`
- `Blazor`
- `visible UI implementation`

Execution agents must scan common MAF projects for these terms and classify each occurrence as generic diagnostics, test data, or domain leak.
