# Prepare solution skeleton subprocess

Launch the generic .NET solution setup subprocess when the slice starts from an empty or incomplete .NET solution. If the skeleton already exists, record explicit proof before skipping.

The child setup process must use the app archetype, target framework, test framework, and product root grounded by project structure or architecture notes. Do not force any UI, API, CLI, worker, library, or test technology choice unless the current run provides that signal. Escalate when the generic setup process cannot select a safe template without inventing requirements.
