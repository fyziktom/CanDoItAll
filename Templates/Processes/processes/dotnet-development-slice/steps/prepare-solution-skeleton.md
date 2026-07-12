# Establish solution baseline subprocess

Launch the generic .NET solution baseline subprocess for either an explicit initialization plan or an explicit existing-solution verification context. The child never chooses between those modes from prose.

When the context says `initialize`, the child may create only the declared solution, application, and test baseline. When it says `verify-existing`, it performs read-only verification of the declared solution and project files; it must not create a helper script, call `dotnet new`, add solution membership, add a project reference, or mutate product files. Do not write `Status: InProgress`, progress notes, or a partial parent artifact while the child run is active. Submit the launch tool's `ParentDeferredOutcomeJson` exactly so the parent waits for the child run and later completes from the child setup handoff.

The child setup process receives the exact `dotnet-solution-context/v1` artifact from `slice-architecture-check`. It must use that bound decision and must not select an app archetype, framework, test framework, solution name, or layout from prose. Escalate the upstream architecture contradiction when that decision is missing or invalid; do not create a replacement contract in this step.

Accept either `setup-handoff` or `setup-handoff-after-repair` from the child setup run as valid baseline proof. Treat `setup-repair-escalation` as blocker evidence, not as accepted baseline proof.
