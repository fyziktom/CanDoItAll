# Codex Implementation Prompt

You are implementing this bundle on branch `maf-processes-refactor`.

Follow the subbundles in numeric order. Do not skip gates. Do not create Process Core or production driver APIs. Do not simplify behavior.

For every movement subbundle:

1. Open exact source references.
2. Confirm current source shape.
3. Add or update focused tests before or with the movement.
4. Move only the targeted logic.
5. Keep compatibility wrappers where existing tests or other partials depend on them.
6. Run the required focused tests.
7. Record proof under the subbundle proof folder.
8. Update the execution report.

Browser validation is N/A unless UI files are unexpectedly changed. Do not run small/medium/mobile proof. If UI proof becomes necessary, use large desktop/PC only and explain why.
