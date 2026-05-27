You are working in `fyziktom/CanDoItAll`, branch `processes-hardening`.

Use this bundle:

`codex/bundles/maf16-processes-real-usage-hardening-v2`

Goal: audit the completed MAF 1.6 upgrade and process runtime fixes, then implement the next layer of hardening so the system actually uses MAF 1.6 advantages where useful.

Non-negotiable rules:

- Do not treat a package version bump as full MAF 1.6 adoption.
- Build a MAF 1.6 feature adoption matrix first.
- Keep the CanDoItAll adapter boundary explicit; do not leak MAF package internals into Processes.
- Keep Processes above Workflows; Workflows remain role executors/subsystems under Processes.
- Do not weaken process artifact validation.
- Do not hardcode Blazor/Tetris into the process core.
- Preserve PostgreSQL-only runtime.
- Every critical subbundle must produce failing-first/adversarial proof, passing proof, source assertions, anti-stub audit, changed-file hashes, and a note about whether behavior is package-only, adapter-level, process-level, or UI-level.
