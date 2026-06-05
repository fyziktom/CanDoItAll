# SB35 Semantic Invariants

- Invariant ID: SB35-INV-001
- Source raw note: Preserve provider recovery behavior while making provider classification, fallback selection, health probing, assigned-agent mutation, and recovery directive assembly explicit.
- Expected behavior: Provider failure detection keeps the same candidate text priority and summary mapping; fallback providers still exclude the failed provider and non-OpenAI/Ollama providers; health probes keep timeout/cancellation behavior; only assigned-agent repair performs `GetAgentEditorAsync`/`SaveAgentAsync`; provider recovery decisions keep provider-failure category, source execution run ID, failure summary, and next-attempt timing.
- Disallowed shallow implementation: A helper that changes provider failure summaries, allows Ollama fallback, hides `SaveAgentAsync` in pure helpers, removes the health-probe timeout, changes fallback model normalization, or changes provider recovery directive content is rejected.
- Failing-first test: N/A - process non-production refactor with no behavior change; bundle://proof/SB35/transcripts/focused-provider-recovery-tests.txt proves provider recovery parity still passes.
- Passing test: bundle://proof/SB35/transcripts/focused-provider-recovery-tests.txt.
- Changed source files: provider helper files and dispatcher wrappers listed in bundle://proof/SB35/manifest.md.
- Production assertions: bundle://proof/SB35/transcripts/source-assertions-and-scans.txt proves helper delegation, line-count movement, no Core/driver tokens, no stubs, and no `SaveAgentAsync` inside pure provider helpers.
- Red-team negative case: bundle://proof/SB35/transcripts/source-assertions-and-scans.txt scans for hidden provider mutation in pure helpers and for Process Core, driver API, TODO, NotImplementedException, and default-return stubs.
- Downstream dependency check: SB36-SB40 may proceed because provider recovery side effects are explicit and helper-owned.
