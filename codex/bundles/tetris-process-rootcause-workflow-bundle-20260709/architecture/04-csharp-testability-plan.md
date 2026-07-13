# C# Testability Plan

## Characterization Tests

- Incident fixture for Tetris QA attempts without LLM.
- Current legacy behavior fixture for templates without completion issue route metadata.
- Existing tests around adapter completion gates and capability scope kept green during behavior-preserving extraction.

## Isolated Unit Tests

- Receipt rule resolver parses:
  - newline strings;
  - JSON string arrays;
  - JSON object arrays;
  - by-step string maps;
  - by-step object maps.
- Receipt evaluator filters by branch outcome, purpose, current-run, success requirement, and minimum count.
- Completion issue router maps generic issue codes and branch outcomes using route metadata.
- Recovery advice providers emit generic and .NET/software-delivery guidance from the right owner.
- Acceptance criteria matrix generator/validator maps project-structure requirements to proof methods.

## Negative Tests

- A repair branch without deterministic defect evidence and without required proof is not accepted.
- A hardcoded `qa-validation` branch route in generic runtime/application fails architecture scan.
- An object receipt rule omitted by launch variable formatting fails parser/launch service test.
- A shell UI with no Tetris-like behavior fails acceptance matrix.
- Duplicate product/capability receipt requirements do not produce duplicate diagnostics.

## Integration/Composition Smoke

- Adapter converts a branch-routed completion issue to a succeeded result with branch signal and runtime gate findings.
- Downstream repair step can read runtime gate findings.
- Template migration loads via process template catalog.
- Workbench contributor emits structured rules and route metadata.
- .NET runtime run/stop fake host records owner/startup/cleanup receipts.

## Fake Provider/Tool/Driver Proof

- Use fake receipt records instead of real MAF runs for unit tests.
- Use fake process host for .NET lifecycle tests.
- Use arbitrary branch names in routing tests to prove generic route behavior.

## Final Confirmation

- Targeted unit tests after each subbundle.
- Full `dotnet test` for unit test project after critical foundations.
- `dotnet build CanDoItAll.slnx` before final closure.
- Refreshed CodeAnalytics dependency/cycle proof before final closure.

## Corrective Test Contract

1. Failing-first source test rejects every partial adapter declaration and the current 20-file cluster.
2. Thin-adapter unit test injects a fake step executor and proves request/result/cancellation delegation.
3. Direct managed-artifact/completion/subprocess tests instantiate their extracted owners without the adapter or full host.
4. Negative architecture test rejects a renamed replacement monolith by enforcing responsibility-specific source/type limits and an adapter member allow-list.
5. Domain-policy tests prove .NET receipt semantics match only in the .NET contributor and unrelated tool families do not match.
6. DI composition smoke resolves both process driver interfaces to the same adapter and proves its executor graph is registered.
7. Existing `ProcessRuntimeIntegrationAdapterTests` remain characterization/integration proof, not the only tests for extracted behavior.
8. Final production E2E verifies automation dispatch, bound agent execution runs, current-run tool receipts/artifacts, and provider usage without manual transitions.

## Persistent Repair Reopen Test Contract

1. Failing-first recovery test contains a stable diagnostic plus incidental diagnostic churn; the second occurrence must require manager action.
2. A genuinely replaced diagnostic may receive the next bounded retry while the global retry budget remains.
3. Unsafe, non-idempotent, policy, and capability diagnostics remain ineligible for automatic repair.
4. Template projection proves `software-delivery/quality-repair` is runtime-owned and exposes only subprocess launch/observation operations.
5. The `.NET quality repair` child has distinct diagnosis, mutation, QA, bughunt, second mutation, recheck, handoff, and no-go artifacts/roles.
6. Revalidation instructions reject any known failed current-run proof as residual risk and require exact failure evidence to feed the next diagnosis.
7. Generic runtime/application source scans reject .NET, Blazor, browser-banner, Tetris, Calculator, work-time logger, SVG, and software-delivery branch literals.
8. Production proof covers four independent app requirements and includes process/agent execution analytics for every retry, branch, manager handoff, or escalation.
