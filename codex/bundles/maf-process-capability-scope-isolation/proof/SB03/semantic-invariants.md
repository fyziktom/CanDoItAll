# SB03 Semantic Invariants

## Invariant MAF-SB03-PROCESS-CONTRACT

- Invariant ID: `MAF-SB03-PROCESS-CONTRACT`
- Source raw note: process steps need a proper channel to limit tools, skills, MCPs, and add scoped instructions.
- Expected behavior: process template JSON, runtime assignments, summaries, launch service, EF persistence, and PostgreSQL migration preserve a strongly typed `CapabilityScope`.
- Disallowed shallow implementation: storing opaque prompt text only, or dropping scope data before the runtime assignment is persisted.
- Failing-first test: `bundle://proof/SB03/transcripts/adversarial-negative.txt` proves capability scope is not assigned through null placeholders.
- Passing test: `Process_template_json_deserializes_capability_scope_contract` in `bundle://proof/SB03/transcripts/passing.txt`.
- Changed source files: `repo://src/Processes/CanDoItAll.Processes.Contracts/ProcessCapabilityScopeModels.cs` with hash `87CAA49FD36664194A9BF85E63A52284A5FE04C31307F24AB9F5E4A910BBBBA9`.
- Production assertions: process assignment persistence serializes `CapabilityScopeJson` and launch materialization copies normalized scope from templates to assignments.
- Red-team negative case: a template with capability directives must round-trip through template loading and persistence instead of becoming prompt-only text.
- Downstream dependency check: SB04 consumes the persisted assignment scope as typed metadata for MAF runtime context assembly.
