# Implementation Prompt

You are implementing `process-dispatch-subprocess-runtime-projection-boundary-v1`.

Work subbundle by subbundle. Do not skip gates. Do not start Process Core. Do not add production process driver APIs. Keep changes module-local under `CanDoItAll.Modules.Processes`.

Preserve subprocess dispatch behavior exactly. Side effects must remain explicit. If a helper writes files, writes EF records, calls `ProcessesService`, or transitions a step, name it as a coordinator/handler and test it accordingly.

Browser proof is N/A unless UI files unexpectedly change. If UI proof is needed, use large desktop/PC only.
