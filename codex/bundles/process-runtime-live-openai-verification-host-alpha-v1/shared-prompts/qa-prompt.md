# QA / Red-Team Prompt

Reject the bundle if:
- live OpenAI is skipped while key is present and no explicit opt-out exists;
- a runtime host executes commands, external calls, or process mutations;
- a selector uses fallback or object payloads;
- manager diagnostics apply transitions/finalizers/retries;
- audit records are not redacted or not hash-backed;
- a source/test path depends on concrete `codex/bundles/<bundle-name>`;
- Process Core references drivers/modules/infrastructure/EF/UI/AgentFramework;
- proof is only status rows or non-empty output.
