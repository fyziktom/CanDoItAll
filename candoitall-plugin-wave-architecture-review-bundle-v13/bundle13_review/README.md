# CanDoItAll plugin-wave architecture review bundle v13

This bundle captured the manual runtime-hardening review after bundle12 and is now fully executed.

## Final status

- Phase10 / phase11 / phase12 carry-forward gates pass on the current repo.
- Phase13 runtime-hardening gate passes on the current repo.
- Bundle13 implementation is complete and validated.
- The repo is now execution-grade for the upcoming plugin wave within the scope of this bundle.

## Closed blockers

1. `AutomationRuntimeOptions` are now bound from `Automation:Runtime` production configuration, including lease and worker backoff settings.
2. Durable idempotency for internal envelopes, ingress envelopes, and connector outbox commands now recovers correctly from uniqueness conflicts under concurrency.
3. Runtime workers now acquire due work through lease-aware DB-side selection and atomic claim boundaries instead of broad in-memory scans.
4. Hosted worker loops now isolate iteration failures, log them, and retry after configured backoff instead of exiting permanently.
5. Production call sites no longer schedule new work through the legacy in-memory queue seam, and the legacy bridge forwards old queue items into the durable runtime plane.

## Validation summary

- `dotnet build CanDoItAll.slnx -v minimal` passed on April 6, 2026.
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~Automation_runtime_options_bind_from_configuration|FullyQualifiedName~Automation_mqtt_bridge_reads_production_configuration_without_test_only_overrides|FullyQualifiedName~Concurrent_message_publish_with_same_dedupe_key_returns_single_envelope|FullyQualifiedName~Concurrent_ingress_accept_with_same_external_message_returns_single_envelope|FullyQualifiedName~Concurrent_connector_enqueue_with_same_idempotency_key_returns_single_command|FullyQualifiedName~Parallel_dispatchers_do_not_process_the_same_delivery_twice|FullyQualifiedName~Parallel_connector_outbox_workers_do_not_process_the_same_command_twice|FullyQualifiedName~Abandoned_delivery_lease_can_be_reclaimed|FullyQualifiedName~Automation_message_pump_worker_continues_after_transient_dispatch_failure|FullyQualifiedName~Connector_outbox_worker_continues_after_transient_processing_failure|FullyQualifiedName~Legacy_background_queue_items_are_forwarded_to_durable_runtime_when_legacy_mode_is_enabled|FullyQualifiedName~PromptFactory_does_not_use_legacy_background_queue_for_new_work" -v minimal` passed, 12/12.
- `python .\candoitall-plugin-wave-architecture-review-bundle-v12\scripts\gate_check_phase10.py C:\repositories\CanDoItAll` passed.
- `python .\candoitall-plugin-wave-architecture-review-bundle-v11\scripts\gate_check_phase11.py C:\repositories\CanDoItAll` passed.
- `python .\candoitall-plugin-wave-architecture-review-bundle-v12\scripts\gate_check_phase12.py C:\repositories\CanDoItAll` passed.
- `python .\candoitall-plugin-wave-architecture-review-bundle-v13\bundle13_review\scripts\gate_check_phase13.py C:\repositories\CanDoItAll` passed.
- `python .\candoitall-plugin-wave-architecture-review-bundle-v13\bundle13_review\scripts\validate_bundle.py --stage prepared C:\repositories\CanDoItAll\candoitall-plugin-wave-architecture-review-bundle-v13\bundle13_review` passed.
- `python .\candoitall-plugin-wave-architecture-review-bundle-v13\bundle13_review\scripts\validate_bundle.py --stage completed C:\repositories\CanDoItAll\candoitall-plugin-wave-architecture-review-bundle-v13\bundle13_review` passed.

## Residual warnings

- Existing `NU1510` warnings remain in `src/CanDoItAll.Mcp.DotNetWatch/CanDoItAll.Mcp.DotNetWatch.csproj`.
- Marker and reference compatibility fallbacks remain active in the Workbench projection layer.
- `CrmHrServices.cs` and `ProjectWorkbenchModels.cs` remain oversized hotspots.

## Contents

- detailed review notes,
- current gate outputs,
- a new `gate_check_phase13.py`,
- execution-grade subbundles `P13-001` through `P13-005`,
- closure proof showing the runtime hardening work shipped and validated.
