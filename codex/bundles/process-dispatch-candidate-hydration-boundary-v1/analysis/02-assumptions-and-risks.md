# Assumptions And Risks

## Critical Path Risks

1. Candidate hydration is a high-risk seam because it shapes the complete `DispatchCandidate` consumed by later execution, projection, validation, and finalizer code.
2. Header selection changes can silently alter which step is claimed first.
3. Technical-agent binding/access preparation includes side effects; treating it as pure would hide behavior and weaken tests.
4. Artifact-input preparation affects prompts and downstream artifact satisfaction. A shallow helper extraction could preserve compile but change prompt semantics.
5. Premature Process Core extraction would likely drag EF entities, AgentFramework models, Workbench/project-structure concepts, or technical-agent binding into the wrong boundary.
6. Premature production driver APIs would freeze vocabulary before candidate/evidence intent semantics are stable.

## Validation Risks

- Focused tests may miss failed-run dispatchability and recovery execution reuse if the filter is too narrow.
- Candidate hydration tests must verify all route kinds: subprocess, workflow-backed role, direct agent, recovery reuse, and missing technical-agent binding.
- Source scans must distinguish documentation-only driver readiness from production driver API broadening.
- Browser validation should remain N/A; any UI screenshot churn is waste for this bundle.

## Reopen Triggers

- Any changed ordering of candidate headers without explicit parity tests.
- Any missing or renamed field in `DispatchCandidate` construction.
- Any hidden `Process Core` or driver-pack project/namespace/API.
- Any direct MAF/product dependency regression.
- Any small/medium/mobile proof artifact.
- Any technical-agent access mutation moved into a helper that lacks side-effect naming and tests.
