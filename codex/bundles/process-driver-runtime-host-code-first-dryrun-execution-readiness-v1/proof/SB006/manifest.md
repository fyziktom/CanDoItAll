# SB006 Proof Manifest

- Gate: Durable audit production proof.
- Source proof: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs`
- Test proof: `Process_verification_audit_store_SB006_INV_003_persists_postgresql_audit_records_across_service_scopes`
- Negative proof: invalid audit time windows reject instead of silently falling back.
- Changed-file SHA-256: `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessVerificationAuditStore.cs` `5681FBDF36EFA2EDD54606F5B79422E6E923C18581FB78EB30CDBFFACD215DE0`
- Result: Passed.
