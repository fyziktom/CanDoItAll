# Implementation Agent Prompt

You are implementing `process-runtime-execution-restoration-live-openai-completion-v1`.

Do not treat the previous `process-runtime-live-e2e-openai-hardening-v1` as completed. It is in progress with SB001-SB012 completed and SB013-SB048 pending. Continue from that state.

Hard rules:
- no transient `codex/bundles/<bundle-name>` references in long-lived `src` or `tests`;
- no generic process-driver runtime host;
- no driver registry/selector/DI auto-registration/manager command;
- no driver scheduler/workflow hook;
- no shell/Graph/file/network/storage/workspace/process mutation through drivers;
- no broad Process Core runtime extraction;
- large desktop only for browser proof.

Close each critical gate with source-backed proof, negative proof, positive proof, anti-stub audit, changed-file hashes and command transcripts.
