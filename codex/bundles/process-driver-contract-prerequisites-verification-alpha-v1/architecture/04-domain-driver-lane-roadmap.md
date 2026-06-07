# Domain Driver Lane Roadmap

## .NET/Rust Transcript Verifier

First candidate after prerequisite gates.
- Inputs: existing build/test/proof transcripts.
- Outputs: read-only diagnostics.
- Denied: running commands, restoring packages, modifying files, writing artifacts, changing process state.

## Runtime Verification Helper

Second candidate.
- Inputs: execution/finalizer/retry/provider descriptors.
- Outputs: consistency diagnostics.
- Denied: retry scheduling, provider repair, execution.

## Business Analysis Gap Reviewer

Later candidate.
- Inputs: process artifacts and requirement/evidence descriptors.
- Outputs: gap notes.
- Denied: business-record mutation, task creation.

## Office Evidence Reviewer

Later candidate.
- Inputs: already-produced artifacts and proof summaries.
- Outputs: review diagnostics.
- Denied: Graph/Office calls, email category mutation, task creation, document writes.
