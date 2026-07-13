# Semantic Invariants - SB08

## INV-SB08-01

- Invariant ID: `INV-SB08-01`
- Source raw note: F09/F11 require template hard gates to move from prose to typed metadata across all affected process and artifact contracts.
- Expected behavior: all nine subprocess parent template steps carry typed runtime-owned subprocess contracts and required output gates.
- Disallowed shallow implementation: hardening only the blocked sample template.
- Failing-first test: `bundle://proof/SB09/transcripts/adversarial-negative.md`
- Passing test: `bundle://proof/SB09/transcripts/final-validation.md`
- Changed source files: `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`, `repo://Templates/Processes/processes/software-delivery/definition.json`, `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessMafHardeningRegressionTests.cs`.
- Production assertions: regression test asserts exactly nine runtime-owned subprocess parents and validates accepted/no-go metadata.
- Red-team negative case: `prepare-solution-skeleton` cannot manually skip without typed output-producing proof.
- Downstream dependency check: SB09 final closure covers template load, bridge, and full process filter.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
| --- | --- | --- | --- | --- |
| Typed process template contracts | template files and loader tests | runtime contract resolver/launch variables | template load to process launch lifecycle | prose-only hard gate fails validation |
