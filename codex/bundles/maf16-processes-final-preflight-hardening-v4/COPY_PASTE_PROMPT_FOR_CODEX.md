You are working in `fyziktom/CanDoItAll`, branch `processes-hardening`.

Use this bundle:

`codex/bundles/maf16-processes-final-preflight-hardening-v4`

Execute subbundles in order.

Hard rules:

- Do not run the full live app-generation process until SB18 says GO.
- Start with source/proof audit. Do not trust prior bundle status blindly.
- Do not claim MAF 1.6 feature adoption unless production code and runtime tests prove it.
- Do not upgrade to MAF 1.7 in this bundle. 1.6.2 remains the current target unless the user explicitly asks for 1.7.
- Expand read-model/finalizer parity beyond ContentUnavailable.
- Do not weaken finalizer validation.
- Keep process core generic and Processes above Workflows.
- Keep Blazor/Tetris logic in templates/profiles/tests/runbooks, not core runtime.
