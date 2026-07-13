# C# Pattern Selection Records

## PSR-001 Launch Variable Resolver

- Pattern: small deterministic service with explicit result type.
- Reasoning: placeholder resolution is pure and testable; a heavier pipeline is not justified.
- Required behavior: bounded passes, cycle detection, unresolved-placeholder diagnostics, and no silent fallback.

## PSR-002 Completion Gate Evaluator

- Pattern: ordered gate strategy list with aggregate result.
- Reasoning: each gate owns one concern, while the evaluator owns deterministic aggregation and priority.
- Required behavior: all gates execute where safe; primary diagnostic is selected by policy without dropping secondary diagnostics.

## PSR-003 Required Tool Receipt Matcher

- Pattern: matcher strategy service.
- Reasoning: receipt matching may vary by exact tool, argument shape, path, and run scope.
- Required behavior: match exact required tool receipts and explain misses.

## PSR-004 Recovery Classifier

- Pattern: explicit policy classifier.
- Reasoning: recovery must be predictable and testable, not substring-based or fallback-based.
- Required behavior: use retry safety, idempotency, diagnostic source, policy, retry budget, and fingerprint.

## PSR-005 Recovery Instruction Builder

- Pattern: builder over structured diagnostics.
- Reasoning: rework packets combine multiple structured facts into concise operational instructions.
- Required behavior: no unresolved placeholders; include do-not-repeat guidance and exact repair target.

## PSR-006 Subprocess State And Artifact Bridge

- Pattern: resolver plus bridge facade.
- Reasoning: child state resolution and artifact slot transfer are separate concerns.
- Required behavior: preserve child diagnostic root cause and require ledger/accepted slot evidence.

## PSR-007 Tool Plan Guard And Executor

- Pattern: command records for deterministic tool actions plus guard/executor services.
- Reasoning: deterministic scaffold/wire/validate steps should be typed operations, not prompt-only text.
- Required behavior: Phase 1 guard existing launch variables; Phase 2 runtime-owned executor after the contract is proven.

## PSR-008 Template Schema Validation

- Pattern: focused validators over typed template records.
- Reasoning: templates need strict structural feedback without runtime coupling.
- Required behavior: execution classes, required receipts, child contracts, artifact slots, and branch/no-go outputs validate without parsing prose.

## PSR-009 Managed Script Lifecycle

- Pattern: one typed command request and concrete workspace executor.
- Reasoning: script write, verification, invocation, receipts, and rooted
  readback are reusable mediated workspace behavior, while the script's domain
  semantics belong to its isolated driver.
- Required behavior: fail closed for malformed plans and outside-root paths;
  preserve current-run receipts; never infer a domain, product topology, or
  process-step name from the script.

## PSR-010 .NET Template Framework Parameter

- Pattern: explicit command parameter at the CLI-adapter boundary.
- Reasoning: a target framework is a distinct `dotnet new` argument, not a
  template-option flag or product-specific scaffolding rule. Encoding it into
  a free-form template string loses validation and can cause the app and test
  projects to use different SDK defaults.
- Rejected alternative: append `--framework` to an opaque template string or
  teach generic process runtime about .NET project files.
- Required behavior: validate the command parameter at the workspace boundary,
  propagate the architecture-selected value through the isolated .NET driver,
  and verify the result through template-owned completion evidence.
