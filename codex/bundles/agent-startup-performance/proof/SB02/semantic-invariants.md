# SB02 semantic invariant proof

| ID | Preserved behavior | Evidence |
| --- | --- | --- |
| SB02-I01 | Revision probes validate current publication contents, canonical revision, duplicated metadata and derived profile fields; no tokens-only availability cache is introduced. | Eight cases of `Warm_single_and_set_lookups_reject_corruption_without_token_changes`, each exercising both warm single and warm set paths; all 16 scenario executions retain all three tokens and reject the old lease. Existing materializer assertions now also compare direct validation availability/shape presence. |
| SB02-I02 | Structurally invalid relationships/snapshots yield no projection; valid but operationally disabled sources/publications retain a disabled projection. | Existing materializer corpus plus `Validate_retains_canonical_shape_for_operationally_disabled_graphs` and `Validate_rejects_missing_and_malformed_graphs_without_effective_profiles`; existing integration unavailable-source/catalog cases. |
| SB02-I03 | Selected local providers still use the original mapper and preserve explicit unsupported-connector/error behavior. | `Revision_probes_preserve_local_mapping_failure_without_token_changes`, local provider initialization/projection cases; unchanged persisted mapper hash. |
| SB02-I04 | Selected shared import cardinality is checked before source conversion, preserving failure precedence even for corrupt fixture data. | `Duplicate_imports_are_rejected_before_invalid_source_materialization`: full load, optimized revision probe and primed registry each return null despite an invalid source ETag when two imports exist. Fixture index definition is restored and compared exactly in `finally`. |
| SB02-I05 | Set-wide typed materialization continues to surface malformed unrelated source conversion failures. | `Revision_set_preserves_invalid_unrelated_source_value_conversion_failure` compares exception types before and after removing the related profile/import; the unrelated source still faults both full and revision set loads. Full-set three reads are intentionally retained. |
| SB02-I06 | Revision values and generation/cache fault/supersession behavior remain unchanged. | `Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries` compares selected and set revisions with full-load oracles; `source-equivalence.json` proves composite revision and GUID writer normalized text equals baseline. Existing snapshot service, preparation and projection failure tests cover faulted/superseded caches and dispatch-time stale selection. |
| SB02-I07 | Credentials remain resolved for each dispatch; no secret is copied into a cache or proof/log. | Unchanged credential dispatch implementation and all ten selected `AgentProviderCredentialDispatchScopeTests`; no new credential reads/registration. Proof copied only logs, SQL command text without parameter values, source/binary identities and sanitized test results. |
| SB02-I08 | Canonical transport/model/price/thinking metadata and network policy projection remain unchanged. | Existing materializer/architecture/integration projection tests; unchanged canonical reader, downstream shared mapper, persisted mapper, contracts and source/import configurations in source hashes. Full materialization consumes the same validated fields. |
| SB02-I09 | Query savings avoid full effective shared-profile construction without narrowing away validation. | Failed-first selected query assertion, then candidate count 2 versus full-load 3; set count remains 3 independently of added providers. Direct allocation test confirms omitted model-copy allocation while canonical validation remains executed. |

The internal validated shape is a short-lived carrier consumed synchronously. It contains references to the existing persisted input entities and is not a deeply immutable cache; no cache is added. Canonical hashing provides integrity validation, not an authenticated publication signature. Supported tracked changes still rotate existing tokens. An out-of-band operationally valid mutation that does not rotate a token has no new invalidation guarantee; the optimization preserves the existing revision contract rather than inventing one.

All named tests are in the selected 76 Unit/35 Integration candidate TRX files. The eight adversarial theory cases also passed before the production change in the 34-case characterization run; they establish behavioral parity, while the separately retained one-case expected failure establishes the query regression assertion.

## Governed invariant contract

### SB02-I01

- Invariant ID: SB02-I01
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Revision probes validate current publication contents, canonical revision, duplicated metadata and derived profile fields; no tokens-only availability cache is introduced.
- Disallowed shallow implementation: Corrupt publication JSON, duplicate metadata or derived profile values while retaining all three tokens; warm single and warm set lookups must reject the graph.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: Eight cases of `Warm_single_and_set_lookups_reject_corruption_without_token_changes`, each exercising both warm single and warm set paths; all 16 scenario executions retain all three tokens and reject the old lease. Existing materializer assertions now also compare direct validation availability/shape presence. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: The original canonical snapshot reader and HasValidProfileCache checks execute before a shape is returned; revision probing has no tokens-only availability branch.
- Red-team negative case: Corrupt publication JSON, duplicate metadata or derived profile values while retaining all three tokens; warm single and warm set lookups must reject the graph.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I02

- Invariant ID: SB02-I02
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Structurally invalid relationships/snapshots yield no projection; valid but operationally disabled sources/publications retain a disabled projection.
- Disallowed shallow implementation: Missing/corrupt graphs must not be replaced by a disabled-but-structurally-valid result; disabled and retired valid graphs must not be omitted.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: Existing materializer corpus plus `Validate_retains_canonical_shape_for_operationally_disabled_graphs` and `Validate_rejects_missing_and_malformed_graphs_without_effective_profiles`; existing integration unavailable-source/catalog cases. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: Validate returns a null shape only for structurally invalid graphs; Materialize still projects operational availability into IsEnabled.
- Red-team negative case: Missing/corrupt graphs must not be replaced by a disabled-but-structurally-valid result; disabled and retired valid graphs must not be omitted.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I03

- Invariant ID: SB02-I03
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Selected local providers still use the original mapper and preserve explicit unsupported-connector/error behavior.
- Disallowed shallow implementation: An unsupported local connector must retain its explicit InvalidOperationException and message instead of returning a revision from tokens alone.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: `Revision_probes_preserve_local_mapping_failure_without_token_changes`, local provider initialization/projection cases; unchanged persisted mapper hash. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: Both selected and set local revisions call the existing MapPersonal mapper.
- Red-team negative case: An unsupported local connector must retain its explicit InvalidOperationException and message instead of returning a revision from tokens alone.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I04

- Invariant ID: SB02-I04
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Selected shared import cardinality is checked before source conversion, preserving failure precedence even for corrupt fixture data.
- Disallowed shallow implementation: Two imports with a malformed source ETag must return null before source conversion, and fixture uniqueness must be restored.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: `Duplicate_imports_are_rejected_before_invalid_source_materialization`: full load, optimized revision probe and primed registry each return null despite an invalid source ETag when two imports exist. Fixture index definition is restored and compared exactly in `finally`. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: The selected relational join requires a single import before a source row can be materialized; two rows cause the existing null outcome.
- Red-team negative case: Two imports with a malformed source ETag must return null before source conversion, and fixture uniqueness must be restored.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I05

- Invariant ID: SB02-I05
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Set-wide typed materialization continues to surface malformed unrelated source conversion failures.
- Disallowed shallow implementation: A malformed unrelated source ETag must still throw during set-wide typed entity conversion after its profile/import are removed.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: `Revision_set_preserves_invalid_unrelated_source_value_conversion_failure` compares exception types before and after removing the related profile/import; the unrelated source still faults both full and revision set loads. Full-set three reads are intentionally retained. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: LoadPersistedProfilesAsync retains all three original typed queries, including the full source dictionary conversion.
- Red-team negative case: A malformed unrelated source ETag must still throw during set-wide typed entity conversion after its profile/import are removed.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I06

- Invariant ID: SB02-I06
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Revision values and generation/cache fault/supersession behavior remain unchanged.
- Disallowed shallow implementation: Profile/import/source token changes, stale generations and faulted cache loads must not reuse an obsolete selection.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: `Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries` compares selected and set revisions with full-load oracles; `source-equivalence.json` proves composite revision and GUID writer normalized text equals baseline. Existing snapshot service, preparation and projection failure tests cover faulted/superseded caches and dispatch-time stale selection. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: CreateCompositeRevision, its GUID writer, the snapshot service and generation behavior are unchanged in source-equivalence/context hashes.
- Red-team negative case: Profile/import/source token changes, stale generations and faulted cache loads must not reuse an obsolete selection.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I07

- Invariant ID: SB02-I07
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Credentials remain resolved for each dispatch; no secret is copied into a cache or proof/log.
- Disallowed shallow implementation: The dispatch credential must not be served from a newly introduced secret cache or copied into diagnostic proof.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: Unchanged credential dispatch implementation and all ten selected `AgentProviderCredentialDispatchScopeTests`; no new credential reads/registration. Proof copied only logs, SQL command text without parameter values, source/binary identities and sanitized test results. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: Credential dispatch source/registrations are unchanged and no new credential retrieval/cache is introduced.
- Red-team negative case: The dispatch credential must not be served from a newly introduced secret cache or copied into diagnostic proof.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I08

- Invariant ID: SB02-I08
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Canonical transport/model/price/thinking metadata and network policy projection remain unchanged.
- Disallowed shallow implementation: Invalid catalog/profile cache fields and restricted network-policy routes must not silently fall back to a permissive projection.
- Failing-first test: This preserves existing behavior, characterized by the retained original baseline/characterization cases; no new behavioral failure is claimed. The optimization failure is separately recorded for SB02-I09 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: Existing materializer/architecture/integration projection tests; unchanged canonical reader, downstream shared mapper, persisted mapper, contracts and source/import configurations in source hashes. Full materialization consumes the same validated fields. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: Full Materialize uses the same validated publication fields and downstream mapper; no public contract or policy schema changed.
- Red-team negative case: Invalid catalog/profile cache fields and restricted network-policy routes must not silently fall back to a permissive projection.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.

### SB02-I09

- Invariant ID: SB02-I09
- Source raw note: bundle://inputs/00-original-request.md and bundle://requirements/01-normalized-requirements.md; preserve startup semantics while reducing validated revision work.
- Expected behavior: Query savings avoid full effective shared-profile construction without narrowing away validation.
- Disallowed shallow implementation: The original concrete selected loader must fail the two-command expectation with actual count3; set-wide count must remain3 as providers are added.
- Failing-first test: Concrete_revision_probes_preserve_full_load_revisions_with_bounded_queries failed with expected2/actual3 in bundle://proof/SB02/transcripts/query-failing-first.md.
- Passing test: Failed-first selected query assertion, then candidate count 2 versus full-load 3; set count remains 3 independently of added providers. Direct allocation test confirms omitted model-copy allocation while canonical validation remains executed. See bundle://proof/SB02/transcripts/integration-passing.md and bundle://proof/SB02/transcripts/unit-passing.md.
- Changed source files: repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedAwareProviderRuntimeProfileSnapshotLoader.cs and repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs; exact owned/context hashes are in bundle://proof/SB02/source-binary-hashes.json.
- Production assertions: The real PostgreSQL command interceptor measures full load3, selected revision2, revision set3; allocation comparison omits effective model copies only.
- Red-team negative case: The original concrete selected loader must fail the two-command expectation with actual count3; set-wide count must remain3 as providers are added.
- Downstream dependency check: Existing Module→ProviderManagement direction is retained; narrow friend-assembly collaboration is recorded in bundle://proof/SB02/architecture-review.md and bundle://proof/SB02/architecture-comparison.json. No new public API/project reference/DI/schema or secret ownership boundary.
