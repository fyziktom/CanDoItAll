# Implementation Agent Prompt

You are implementing `process-driver-verification-alpha-dotnet-rust-core-stabilization-v1`.

Execute phases in order. Do not skip critical gates. Preserve runtime behavior. Keep all driver work verification-only unless a subbundle explicitly says otherwise; no subbundle in this bundle approves runtime registry, DI, manager commands, shell execution, Graph/Office calls, workspace/storage writes, or process mutation.

After each critical gate, update `reviews/01-execution-report.md`, write proof transcripts, run source scans, and stop if any forbidden dependency or runtime token appears.
