# Implementation prompt for Codex

Implement **all** phase11 subbundles in this bundle.

Rules:
- keep code comments in English,
- do not ask for clarification unless absolutely blocked by missing repository content,
- do not close any item with stubs or UI-only placeholders,
- update startup/DI so the new runtime is actually active,
- keep Quartz behind platform seams,
- keep MQTT optional,
- do not model operational envelopes as default Workbench nodes,
- add all required tests using the exact names from this bundle,
- update `scripts/gate_check_phase11.py` if and only if you intentionally rename the required exact types.

You must return:
1. the code changes,
2. the added tests,
3. the gate output,
4. a concise explanation of how each hard gate was closed.
