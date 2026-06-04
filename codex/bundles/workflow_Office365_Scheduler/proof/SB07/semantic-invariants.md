# Semantic Invariants SB07

- No shallow-pass proof: integration tests cover real Scheduler handler persistence plus production workflow launcher route parsing, not only test fakes.
- No live Office365/Graph dependency in automated tests: Office365 external-write policy is verified through the plugin catalog descriptor.
- No silent external write approval bypass: scheduled workflows that reach approval-required external effects remain `WaitingForApproval`; no Scheduler path grants unattended mark-processed permission.
- No-message runs are terminal no-action successes and do not overwrite an existing `LastError`.
- Waiting-for-approval runs are terminal for Scheduler retry/dedupe and are not retried as failures.
- Graph/network and project-write failures persist explicit retry categories.
- Code comments must be in English.
