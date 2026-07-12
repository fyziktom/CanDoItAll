# Prepare solution skeleton subprocess

Launch the generic .NET solution setup subprocess when the slice starts from an empty or incomplete .NET solution. If the skeleton already exists, record explicit proof before skipping.

When the solution/app/test skeleton is missing or incomplete and this step has the governed subprocess launch tool, launch the setup subprocess before writing the primary parent artifact. Do not write `Status: InProgress`, progress notes, or a partial parent artifact while the child setup run is active. Submit the launch tool's `ParentDeferredOutcomeJson` exactly so the parent waits for the child run and later completes from the child setup handoff.

The child setup process must use the app archetype, target framework, test framework, and product root grounded by project structure or architecture notes. Do not force any UI, API, CLI, worker, library, or test technology choice unless the current run provides that signal. Escalate when the generic setup process cannot select a safe template without inventing requirements.

Accept either `setup-handoff` or `setup-handoff-after-repair` from the child setup run as valid scaffold proof. Treat `setup-repair-escalation` as blocker evidence, not as accepted setup proof.
