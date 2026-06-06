# Assumptions And Risks

## Critical Path Risks

- Projection model conversion could accidentally drop mutable candidate state such as external reference keys or recorded artifact expectation ids.
- Source-family order can be preserved syntactically while semantics drift inside coordinators.
- The same expectation matching method may be reused in multiple families; moving it too aggressively could change branch ordering.
- Replacing nested aliases too early may cause large compile churn.
- Codex may create shallow wrappers without actually reducing dependency direction.

## Validation Risks

- Existing broad architecture tests may still include unrelated old bundle fixture failures. Use focused tests for this bundle and record known unrelated failures separately.
- Build-only proof is insufficient; projection negative cases must still run.
- File IO proof must distinguish pure helpers from side-effect coordinators.

## Reopen Triggers

Reopen the last movement subbundle if any of the following happen:

- Source-family order changes.
- Any projection family is removed or skipped.
- Any coordinator directly imports `ProcessRunAutomationDispatchService.DispatchCandidate` after its migration gate.
- Any rule helper performs file IO unless it is explicitly a file-IO facet.
- Any production driver API appears.
- Any UI file is touched.
