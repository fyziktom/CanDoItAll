# Master prompt for Codex

You are a senior C# architect working on CanDoItAll process runtime hardening.

The previous Calculator scenario now passes. The current Tetris escalation is not a basic scaffolding failure. It exposes a generic process/runtime contract bug: QA can find a repairable product defect, but the adapter still enforces acceptance-only runtime/browser receipts and exhausts the same-step retry budget instead of routing the configured repair branch.

Do not implement a Tetris-specific fix. Do not hardcode `qa-validation`, `quality-accepted`, `repair-required`, Blazor, or `.NET` terms in generic runtime/dispatcher logic. Generic code may handle branch outcome keys and rule metadata as data. .NET and software-delivery details belong in templates, the Workbench DotNet contributor, or a domain-specific recovery advice provider.

Implement the work in small subbundles:

1. Add incident regression tests.
2. Extract completion gate evaluator into testable services without behavior changes.
3. Add branch-aware structured receipt rule parsing with legacy compatibility.
4. Apply branch-aware receipt enforcement and deduplication.
5. Add branch-routable completion issue handling.
6. Move domain-specific recovery advice out of the generic builder.
7. Harden software-delivery templates and prompt wording.
8. Add project-structure acceptance criteria matrix support.
9. Harden .NET runtime tool lifecycle.
10. Add observability and architecture guardrails.

After every subbundle, run the relevant unit tests. At the end, run the full unit/integration suite available in the repository.
