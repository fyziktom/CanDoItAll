# Run the .NET quality repair subprocess

Launch and observe `dotnet-quality-repair` with `project_structure_process_subprocess_launch`. Pass the concrete QA disposition, reviewed implementation evidence, exact failed command or runtime/browser evidence, and approved product boundary into the child request.

Pass the required `scope-boundary-packet` as authoritative child context. The QA defect packet may narrow the immediate repair action, but it must not replace or discard original core acceptance obligations. A proof-only correction is valid only when current product-source and behavioral evidence already closes those obligations.

This parent step does not diagnose the defect, mutate product files, run validation, launch the product, or capture browser proof. Those responsibilities belong to distinct child roles. Persist `ParentDeferredOutcomeJson` while the child is active, return the deferred outcome, and do not wait silently or create a duplicate child run.

Accept only a child artifact from `quality-repair-handoff` or `quality-repair-handoff-after-bughunt`. Treat `quality-repair-no-go` as blocker evidence for manager escalation, never as a repaired change set. A completed child run without a mapped accepted handoff is not successful repair proof.
