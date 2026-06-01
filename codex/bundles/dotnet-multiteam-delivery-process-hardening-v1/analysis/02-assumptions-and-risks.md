# Assumptions And Risks

## Assumptions

- The stable process key `software-delivery` should remain the default multi-team process, but its contract can become .NET-oriented for this phase.
- JavaScript delivery will get a separate process later and is out of scope now.
- App-type recognition can be represented as a required contract artifact rather than hardcoded runtime branching.
- Process-run node creation already exists in the project-structure bridge; template instructions only need to target its child hierarchy.

## Critical Path Risks

- If subprocess references are added without updating manifest/default warmup ordering, importing default templates can fail.
- If writeback steps use `ExternalProductTargetMutable`, screenshot/runtime writeback agents could accidentally get product mutation rights.
- If screenshot instructions only say "attach screenshots" without naming the process-run `Screenshots` parent, agents will keep storing images under mixed delivery nodes.
- If runtime command writeback is folded into QA prose, smaller models may omit it after passing build/test proof.

## Validation Risks

- Template JSON can be syntactically valid while operation contracts are semantically wrong; targeted governance assertions are required.
- Process projection recomposition can fail or overlap nodes after new subprocesses are added; component projection tests should be part of validation.
- Existing tests assert the old `software-delivery` implementation shape and must be updated to the new subprocess-backed model.

## Reopen Triggers

- Reopen SB02 if any architecture, QA, screenshot, classification, or writeback step gains `MutateProductTarget`.
- Reopen SB02 if `software-delivery` imports before its child subprocess definitions can resolve.
- Reopen SB03 if screenshot writeback does not explicitly create/use `Screenshots` under the process run node.
- Reopen SB03 if runtime command writeback does not require both `Run app` and `Run tests`.
- Reopen SB04 if targeted process-template tests pass only by checking strings without checking typed operation contracts.
