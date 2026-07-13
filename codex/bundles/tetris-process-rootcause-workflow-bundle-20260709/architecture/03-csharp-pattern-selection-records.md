# C# Pattern Selection Records

## PSR-01 Evaluator Extraction

- Problem force: completion gate behavior is embedded in a broad adapter partial class and cannot be tested without MAF integration.
- Selected pattern: service extraction with a thin adapter facade.
- Rejected simpler option: add more private helper methods to the partial adapter. This keeps responsibility and testability problems.
- New types/projects: `ProcessCompletionGateEvaluator`, context/evaluation records in existing process/module namespace chosen by SB01.
- Dependency direction: evaluator receives dependencies/data from adapter; evaluator does not call MAF execution.
- Unit-test seam: service-level tests pass assignment/output/receipts directly.
- Migration plan: extract behavior-preserving evaluator first, then add branch/routing behavior.
- Proof required: old adapter shrinks or delegates; extracted tests do not instantiate full MAF runtime.

## PSR-02 Receipt Rule Resolver

- Problem force: product receipt rules exist as string arrays and must evolve without breaking legacy templates.
- Selected pattern: parser/normalizer service plus typed rule record.
- Rejected simpler option: regex over JSON strings. This would be stringly typed and brittle.
- New types/projects: `ProcessCompletionReceiptRuleResolver`, `ProcessCompletionRequiredToolReceiptRule`.
- Dependency direction: contracts define serialized rule shape; resolver consumes launch variables and template metadata.
- Unit-test seam: feed legacy and structured JSON directly.
- Migration plan: support old formats first, then Workbench emits object arrays.
- Proof required: legacy string tests remain green; object-rule tests prove branch applicability.

## PSR-03 Completion Issue Router

- Problem force: completion issues need three outcomes and must be driven by template metadata.
- Selected pattern: strategy/router service.
- Rejected simpler option: switch on issue code and step key in adapter. This would hardcode domain behavior in generic code.
- New types/projects: `ProcessCompletionIssueRouter`, route metadata records.
- Dependency direction: router depends on generic metadata; templates/Workbench provide domain route data.
- Unit-test seam: route table fixtures with arbitrary branch names.
- Migration plan: no route metadata preserves existing behavior.
- Proof required: accepted-branch content failure routes repair without retry budget consumption.

## PSR-04 Recovery Advice Provider

- Problem force: generic recovery builder contains .NET and QA branch guidance.
- Selected pattern: provider strategy.
- Rejected simpler option: keep constants but move them to helper methods. This still leaks domain behavior.
- New types/projects: `IProcessRecoveryAdviceProvider`, generic provider, Workbench .NET software-delivery provider.
- Dependency direction: generic builder orchestrates providers; Workbench implementation registered externally.
- Unit-test seam: provider selection and output guidance tested independently.
- Migration plan: preserve behavior while moving domain text out.
- Proof required: forbidden-token scan against generic files passes.

## PSR-05 Acceptance Criteria Matrix

- Problem force: project-structure requirements are free text and QA can accept shell UI.
- Selected pattern: artifact-backed criteria matrix.
- Rejected simpler option: longer prompt text. Prompt-only guidance already failed.
- New types/projects: matrix artifact contract and generator/validator services or template metadata as implementation discovers.
- Dependency direction: Workbench/project-structure owns extraction from project structure; process templates consume matrix ids.
- Unit-test seam: Calculator-like and Tetris-like fixture matrices.
- Migration plan: add generic artifact, wire to implementation/review/QA/repair/recheck.
- Proof required: Tetris-like shell fails matrix criteria; Calculator-like simple case remains low overhead.

## Corrective Record: Thin Adapter Facade

- Problem force: one driver boundary type owns unrelated integration, artifact, completion, subprocess, and recovery behavior across partial files.
- Selected pattern: thin facade delegating to a cohesive agent step executor and completion/subprocess/artifact collaborators.
- Rejected simpler option: more extracted methods or another partial file cannot create independent ownership or tests.
- Dependency direction: adapter depends inward on module services; extracted services never depend back on the adapter.
- Test seam: fake step executor proves boundary delegation; each collaborator is constructed directly.
- Migration proof: zero adapter partial declarations and no forwarding methods for moved behavior.

## Corrective Record: Strategy Catalog For Tool Receipt Semantics

- Problem force: generic completion logic branches on domain tool families and software-delivery step keys, while additional app/runtime families may appear.
- Selected pattern: a small strategy contribution contract composed into an immutable catalog.
- Rejected simpler option: a static .NET helper called by generic code still couples the generic policy to the domain; a switch recreates the leak.
- Dependency direction: generic evaluator depends on the contribution contract; composition knows concrete contributors.
- Test seam: generic, .NET, and unrelated-tool cases run without adapter/workspace/provider construction.
- Migration proof: forbidden-token scan plus positive .NET contribution behavior.

## PSR-06 Persistent Diagnostic Progress Classification

- Problem force: an aggregate diagnostic batch changes when incidental findings change, masking an unchanged blocker and causing blind retries.
- Selected pattern: value-object comparison over stable diagnostic identities within the existing recovery classifier, with bounded policy driven by options.
- Rejected simpler option: lower only the global retry count. That would stop genuinely progressing repairs and would not identify why progress stalled.
- Dependency direction: runtime consumes generic diagnostic receipts only; producers retain responsibility for issuing stable code/evidence identities.
- Unit-test seam: arbitrary codes/hashes prove persistent overlap, replacement, unsafe diagnostics, and global-budget behavior.
- Migration proof: one unchanged diagnostic routes to manager after one retry even when other diagnostics churn.

## PSR-07 Runtime-Owned DotNet Quality Repair Process

- Problem force: one repair agent interprets evidence, edits the product, validates itself, and can misclassify known failures as residual risk.
- Selected pattern: process-level pipeline/chain of responsibility with manager diagnosis, developer mutation, independent QA branch, specialist bughunt, one bounded re-repair, and explicit no-go output.
- Rejected simpler option: append more text to the existing monolithic `quality-repair` prompt. The failed run already had strong prompt text and still edited unrelated files.
- Dependency direction: generic subprocess runtime consumes a typed contract; the .NET driver selects the child process; templates own domain workflow and roles.
- Test seam: catalog projection, contract mapping, branch graph, accepted/no-go artifact bridge, and forbidden-token architecture scans.
- Migration proof: the parent quality-repair step launches/observes only; child mutation steps cannot self-accept; QA cannot accept known failing proof.
