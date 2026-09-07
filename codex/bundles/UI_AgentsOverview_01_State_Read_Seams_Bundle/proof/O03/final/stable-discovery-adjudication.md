# Stable discovery adjudication

Frozen discovery stays unchanged. Seven source-reviewed typed MemberData methods explain all 55 additional cases. Six unchanged methods account for XML/plain-text secret masking or Unicode surrogate representation differences; no method is missing or extra. No test/filter/production change or broad rerun is made.

All 10236 cases passed; zero failures, skipped/not-executed, errors or aborted cases. The pre-run discovery has 10181 entries, not 10236 expanded cases. This receipt does not overwrite that original observation. The seven focused Overview/compatibility selections independently have exact expanded-name equality.

| Public method | Discovery / execution | Explanation |
|---|---:|---|
| CanDoItAll.Memory.Tests.Runtime.MemoryOperationHandlerTests.Capability_mismatch_denial_is_consistent_for_all_handler_callers | 1 / 5 | Deferred MemberData with a nonserializable typed argument; one discovery entry expands at execution. |
| CanDoItAll.Memory.Tests.Runtime.MemoryOperationHandlerTests.No_provider_denial_is_consistent_for_all_handler_callers | 1 / 5 | Deferred MemberData with a nonserializable typed argument; one discovery entry expands at execution. |
| CanDoItAll.Memory.Tests.Security.MemoryOperationAccessAuthorizerTests.Any_changed_ownership_dimension_is_denied | 1 / 9 | Deferred MemberData with a nonserializable typed argument; one discovery entry expands at execution. |
| CanDoItAll.Tests.Integration.External.PluginCatalogIntegrationTests.packaged_plugin_preview_simulation_avoids_live_external_effects | 1 / 6 | Deferred MemberData with a nonserializable typed argument; one discovery entry expands at execution. |
| CanDoItAll.Tests.Unit.AgentFramework.AgentToolInvocationPolicyTests.ProtectApprovalArgumentsForAudit_does_not_trust_spoofed_managed_envelopes | 2 / 2 | Required secret masking operates on plain discovery text versus XML-escaped TRX text; sanitized display is intentionally not byte-equal. |
| CanDoItAll.Tests.Unit.AgentFramework.AgentToolInvocationPolicyTests.ProtectApprovalArgumentsForAudit_fails_closed_for_non_object_json | 2 / 2 | Required secret masking operates on plain discovery text versus XML-escaped TRX text; sanitized display is intentionally not byte-equal. |
| CanDoItAll.Tests.Unit.AgentFramework.FloatingAgentChatSettingsValidatorTests.Validate_rejects_values_outside_inclusive_boundaries | 1 / 8 | Deferred MemberData with a nonserializable typed argument; one discovery entry expands at execution. |
| CanDoItAll.Tests.Unit.AgentFramework.MafFinalizerToolFactorySchemaCharacterizationTests.CreateCapture_produces_the_policy_tool_name_description_and_json_schema | 1 / 8 | Deferred MemberData with a nonserializable typed argument; one discovery entry expands at execution. |
| CanDoItAll.Tests.Unit.AgentFramework.MafProviderAgentFactoryEmptyCompletionCompositionTests.CreateFrameworkAgent_RejectsInvalidEndpoint | 5 / 5 | Required secret masking operates on plain discovery text versus XML-escaped TRX text; sanitized display is intentionally not byte-equal. |
| CanDoItAll.Tests.Unit.AgentFramework.WorkflowExecutorPolicyObservabilityTests.Redaction_removes_secrets_from_json_string_values | 2 / 2 | Required secret masking operates on plain discovery text versus XML-escaped TRX text; sanitized display is intentionally not byte-equal. |
| CanDoItAll.Tests.Unit.Processes.ProcessRuntimeToolPreflightServiceTests.Process_starting_tool_contracts_have_deterministic_host_routes | 1 / 21 | Deferred MemberData with a nonserializable typed argument; one discovery entry expands at execution. |
| CanDoItAll.Tests.Unit.ProviderHistoryLifecycleTests.Detail_bound_preserves_unicode | 4 / 4 | VSTest Unicode surrogate display |
| CanDoItAll.Tests.Unit.ProviderHistoryLifecycleTests.Stream_byte_count_handles_split_surrogate_pairs | 2 / 2 | VSTest Unicode surrogate display |

The initially prepared private hygiene/retention guards assumed one discovery row per execution. They had not executed. The guards are corrected to require this explicit source/hash-bound adjudication plus successful counters. Original guard scripts are retained under pre-adjudication-guards. Repository tests, filters, validation tooling and security rules are unchanged. Raw original transcript hashes remain in stable-summary.json; synthetic secret arguments are masked before retention.
