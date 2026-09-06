# Executable verification lifecycle map

All cases below ran in the fresh owning selection recorded in `proof/validation.json`. Parameterized rows cover both named semantics. Counts are discovery checks for this execution, never architecture invariants.

| Required topic | Executable case (class.method) |
| --- | --- |
| Exact selected set; order/duplicates | ProviderVerificationHardeningTests.Synchronize_verification_requires_exact_selected_set |
| Requested subset rejected | ProviderVerificationHardeningTests.Synchronize_verification_rejects_requested_subset |
| Empty request rejects nonempty current | ProviderVerificationHardeningTests.Synchronize_verification_rejects_nonempty_current_for_empty_request |
| Exact empty accepted | ProviderVerificationHardeningTests.Synchronize_verification_accepts_exact_empty_selection |
| Retired imports ignored | ProviderVerificationHardeningTests.Synchronize_verification_ignores_retired_imports_in_selected_set |
| Revision/time/status evidence | ProviderVerificationHardeningTests.Synchronize_verification_keeps_existing_revision_and_time_evidence |
| Failed/stale source remains locked | SharedSourceRecoveryTests.Failed_source_verification_remains_visibly_unresolved; Disposed_source_verification_emits_no_publication |
| Publish and Unpublish postconditions | SharedTargetVerificationTests.Publication_verification_requires_exact_postcondition |
| Exact canonical alias and enabled | SharedTargetVerificationTests.Imported_settings_verification_requires_exact_alias_and_enabled_state |
| Changed but different values rejected | SharedProviderDeliveryHardeningTests.Imported_settings_verification_rejects_changed_but_different_values |
| Exact retirement identity/state | SharedTargetVerificationTests.Retirement_verification_requires_exact_import_identity_and_retired_state |
| Exact before permits deliberate action | SharedTargetVerificationTests.Exact_before_state_allows_deliberate_retry_without_replay; SharedDeliveryReconstructionTests.Exact_before_state_allows_deliberate_action_without_replay |
| Intervening revision stays unresolved | ProviderRecoveryIntegrationTests.Canonical_target_verification_requires_exact_local_settings_and_publication_state |
| Wrong/stale target cannot unlock | SharedTargetVerificationTests.Wrong_target_or_publication_identity_cannot_unlock; SharedProviderRecoveryTests.Stale_sharing_verification_cannot_unlock_other_target |
| Authoritative tokens refreshed | SharedProviderRecoveryTests.Imported_settings_verification_reloads_tokens_without_replay |
| Target callback failure retains delivery | SharedProviderDeliveryHardeningTests.Known_commit_callback_failure_retains_pending_target_delivery |
| Source callback failure retains delivery | SharedDeliveryReconstructionTests.Known_commit_callback_failure_retains_pending_source_delivery |
| Retry without backend replay | SharedDeliveryLifecycleTests.Callback_failure_retains_pending_delivery_without_repeating_mutation; both rendered callback-failure cases above assert mutation call counts |
| Reconstruction resumes | SharedDeliveryReconstructionTests.Component_recreation_can_resume_pending_target_delivery; source callback-failure theory with recreate=true |
| Acknowledgement prevents repeat | SharedDeliveryLifecycleTests.Successful_acknowledgement_prevents_second_delivery_and_releases_attempt; Receiver_acknowledgement_survives_sender_teardown_without_duplicate_callback |
| Target A cannot acknowledge B | SharedDeliveryLifecycleTests.Target_A_delivery_cannot_be_acknowledged_by_target_B |
| Source A cannot acknowledge B | SharedDeliveryLifecycleTests.Source_A_delivery_cannot_be_acknowledged_by_source_B |
| Concurrent retry serialized | SharedDeliveryLifecycleTests.Concurrent_delivery_retry_is_serialized |
| Disposed/stale emits no callback | SharedDeliveryLifecycleTests.Disposed_or_stale_component_emits_no_new_callback; SharedSourceRecoveryTests.Disposed_source_verification_emits_no_publication |
| Terminal provider cannot resurrect | ProviderVerificationHardeningTests.Terminal_provider_attempt_cannot_be_resurrected |
| Stale provider completion cannot clear newer | ProviderVerificationHardeningTests.Stale_provider_completion_cannot_clear_newer_attempt |
| Terminal shared cleanup | SharedDeliveryLifecycleTests.Successful_acknowledgement_prevents_second_delivery_and_releases_attempt; Terminal_source_attempt_releases_bookkeeping_without_clearing_newer_attempt; Stale_shared_completion_cannot_clear_newer_attempt |
| Pending survives unrelated cleanup | SharedDeliveryLifecycleTests.Pending_delivery_survives_cleanup_of_unrelated_attempts; Known_source_commit_revokes_controlled_retry_until_delivery |
| 02D guarantees retained | ProviderRecoveryIntegrationTests (19 cases); SharedProviderRecoveryTests (5); SharedSourceRecoveryTests (7), plus full exact owning plan |
| Real database Replace semantics | ProviderRecoveryIntegrationTests.Canonical_sync_verification_requires_exact_selected_set |
| Real parent failure and editable draft | SharedDeliveryReconstructionTests.Parent_catalog_failure_keeps_delivery_pending_and_retry_retains_local_draft |

Unit sources: tests/Unit/CanDoItAll.Tests.Unit/AgentFramework. Component sources: tests/Components/CanDoItAll.Tests.Components (new recovery files at project root; namespace remains AgentFramework). Integration source: tests/Integration/CanDoItAll.Tests.Integration/SharedProviders/ProviderRecoveryIntegrationTests.cs. Use the exact discovered names in the archived TRX for execution identity.
