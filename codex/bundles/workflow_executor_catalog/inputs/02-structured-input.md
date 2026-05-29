# Structured Input

## Raw Notes

| Raw note id | Exact wording | Normalized requirements | Owning subbundles |
| --- | --- | --- | --- |
| RN01 | Review the pushed Codex implementation after the workflow MAF hardening follow-up and prepare the next execution bundle focused on fixing remaining runtime/catalog correctness issues. | R1, R12 | SB01, SB08, SB10 |
| RN02 | Expand workflow executors and helper nodes users will obviously need. | R3, R5, R6, R7, R8, R9, R10, R11 | SB03-SB10 |
| RN03 | Make local workspace/folder/file workflows practical and verify whether there are nodes for local folder/file work. | R3, R4, R10, R11 | SB03, SB07, SB09, SB10 |
| RN04 | Improve workflow authoring UX and template coverage. | R10 | SB09, SB10 |
| RN05 | Keep MAF 1.8 alignment stable without overbuilding durable production runtime too early. | R12 | SB01, SB08, SB10 |

## Scope Decision

- This is an initiative-profile bundle because it spans workflow validation, runtime services, executors, templates, UI authoring, and regression proof.
- DurableTask and Azure Functions production runtime remain out of scope unless a failing invariant proves they are needed to keep current in-process behavior honest.
- `command.process` remains blocked unless a bounded, approval-gated host command policy already exists and can be proved safely.

