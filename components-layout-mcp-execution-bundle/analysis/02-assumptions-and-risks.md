# Assumptions And Risks

## Assumptions

- The current BaseLib layout behavior is the intended direction and should be preserved while we move the examples and document the pattern.
- A dedicated sandbox page is better than leaving comparison scaffolding in a product page.
- Future agents will benefit from MCP guidance that explains when to use `Grid`, `Row`, `Column`, and `Stack` together instead of reaching for custom flex wrappers.

## Critical Path Risks

- If the sandbox page does not reflect the actual BaseLib behavior, the MCP guidance and installer work will publish misleading advice.
- If the components MCP is enriched without proving its install path, future agents may be told to use a server that is not actually present in local config.

## Validation Risks

- The shared watch backend may again require a real browser hit before WASM runtime confirmation settles.
- The component MCP output is structured content, so proof should include either tool-harness output or a direct local server invocation, not only source inspection.
- The sandbox app may need a direct run rather than the Zyphonote watch loop for proof.

## Reopen Triggers

- Reopen the BaseLib layout guidance subbundle if the sandbox page reveals that the current `Row` or `Column` semantics still produce unexpected wrapping or alignment.
- Reopen the installer subbundle if the reinstall script updates config but the component MCP cannot actually be launched locally afterward.
- Reopen the skill/plugin subbundle if the guidance points to tools or install steps that differ from the shipped repo behavior.
