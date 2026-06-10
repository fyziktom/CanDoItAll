# Gaps, Risks, and Reopen Triggers

## Critical gaps
1. Live OpenAI proof is not a live process-run proof.
2. `ProcessVerificationRuntimeHost.Verify` is synchronous and has no cancellation token.
3. Expected invalid/empty lane cases throw exceptions instead of returning a structured denial envelope suitable for manager/API/UI use.
4. Audit persistence is in-memory only and has no durable read/query surface.
5. Host lane enablement is static; there is no options-driven disable/emergency-stop model.
6. Manager command service is internal and not yet connected to a stable API/UI operator path.
7. Scheduler/workflow verification readiness is documented but not source-backed by a safe read-only scheduling/use-case test.
8. Live provider policy is partly enforced by command wrapper/proof, not fully by production/test code.
9. Runtime host naming and source scans must clearly distinguish verification-only host from execution-capable driver host.
10. Future execution-capable driver prerequisites remain documented but not executable governance.

## Critical path risks
- A host beta can accidentally become a mutation-capable driver runtime through small API leaks.
- A registry/selector can become fallback routing if unsupported lanes are silently accepted.
- DI can become auto-discovery if implementation adds assembly scanning or service collection hooks.
- Manager commands can accidentally apply recovery/finalizer/transition actions.
- Live OpenAI tests can leak prompts/secrets or become flaky without strict budget, timeout, and marker policy.
- Durable audit persistence can store unredacted sensitive content if not normalized centrally.
- Process Core can be polluted by domain driver concerns if convenience references are added.

## Validation risks
- Build/unit/focused tests are not enough for live-provider behavior.
- A live specialist-agent test is not sufficient proof for process runtime execution.
- Deterministic process scenarios can hide provider integration errors.
- In-memory audit tests can pass while durable audit semantics are missing.
- Report-only proof can claim runtime host readiness while runtime remains internal-only.

## Reopen triggers
- Any source/test reintroduces `codex/bundles/<specific-bundle>` as a long-lived dependency.
- Live process-run smoke is skipped and claimed as provider functionality pass.
- Any driver host API exposes shell, file, storage, workspace, network, Office/Graph, process mutation, transition/finalizer/retry/claim mutation.
- Selector chooses a different lane than requested or falls back silently.
- Audit records include unredacted secrets, emails, prompts, or API keys.
- Process Core references driver packages or process module.
- Manager command or API mutates process state.
