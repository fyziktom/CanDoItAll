You are working in `fyziktom/CanDoItAll`, branch `processes-hardening`.

Use this bundle:

`codex/bundles/maf16-real-adoption-process-proof-v3`

Execute all subbundles in order.

Hard rules:

- Do not run the full real user live process test until this bundle's preflight gate passes.
- Do not treat MAF 1.6 package references as equivalent to MAF 1.6 feature adoption.
- Verify MAF 1.6 symbols/features by compile/reflection tests, not only source grep.
- If a feature is not present in the installed package set, record it as Deferred/Unavailable with proof and a safe fallback.
- Do not upgrade to MAF 1.7 automatically. NuGet may show 1.7.0 as latest, but this branch target is 1.6.2 unless a separate version-policy subtask explicitly changes it.
- Fix process artifact dedupe scope if the source still dedupes by process run only.
- Do not weaken process artifact validation.
- Keep Processes above Workflows.
- Keep the process core generic; Blazor/Tetris belongs in template/profile/test/runbook layers.
