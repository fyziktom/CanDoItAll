# Frozen behavior matrix before implementation

C00 component tests in AgentCapabilitiesReadLifecycleTests (4 cases):
- Pending_target_read_does_not_render_the_previous_editor (negative before fix).
- Late_read_failure_does_not_break_the_new_target (negative before fix).
- Disposing_panel_cancels_the_owned_read (negative before fix).
- Assignment_failure_keeps_uncommitted_local_attachment (characterization; defect assigned to child 02).

Controlled AgentCapabilitiesSurfaceTests (16 cases; each create kind is a separate theory case):
- Renders_loading_without_services
- Renders_no_agents_without_services
- Renders_selected_agent_and_capabilities_without_services
- Renders_no_capabilities_state
- Emits_select_agent_intent
- Emits_assignment_intent
- Emits_verification_intent
- Emits_details_intent
- Emits_each_create_kind_intent (Tool, McpServer, Skill)
- Emits_access_preview_intent_with_typed_draft
- Emits_curator_intent_only_when_ready
- Search_and_filter_state_remains_local
- Snapshot_refresh_preserves_local_filters
- Selected_capability_state_is_controlled_by_parent_snapshot

AgentCapabilitiesSessionTests (12 Unit cases):
- Initial_requested_agent_loads_exact_target
- Missing_initial_requested_agent_fails_closed
- Valid_selection_then_missing_request_clears_authoritative_selection
- Late_A_read_cannot_replace_B
- Late_A_failure_cannot_fail_B
- Disposal_cancels_owned_reads
- Refresh_preserves_current_valid_selection
- Refresh_missing_selected_agent_fails_closed
- Selected_agent_read_failure_clears_prior_editor
- Wrong_editor_identity_fails_closed
- Superseding_selection_cancels_the_prior_read
- Presentation_snapshot_owns_mutable_collections

AgentCapabilitiesHostTests (8 Components cases):
- SelectedAgentChanged_matches_the_authoritative_selection
- ContextAccessStateChanged_deduplicates_equivalent_state
- Exact_managed_curator_can_be_launched
- Spoofed_curator_remains_disabled
- Assignment_and_verification_use_existing_services_once
- Access_preview_preserves_typed_rule_and_renders_result
- Details_and_each_setup_kind_open_through_the_host (three kind cases)
- Late_effect_cannot_publish_into_a_new_selection

The final host discovery is 10 because the dialog theory has three cases. New Components expected: 4 + 16 + 10 = 30; new Unit expected: 12. Counts are execution inventory, never architecture assertions. Freeze exact existing owning selections from current source before executing them; retain discovery listings and actual commands.

Required compatibility topics map to the real production workspace composition test, the existing two AgentCapabilityList tests, the four capability cases in AgentPanelSelectionFailClosedTests, the existing CapabilitySetupFlowServiceTests panel case and affected page/context consumers. Any additional direct adapter test must be recorded before execution.
