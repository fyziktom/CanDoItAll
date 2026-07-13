# Requirement Traceability

| Requirement | Owned by | Source evidence | Closure proof expected |
|---|---|---|---|
| R1 Typed Step Capability And Proof Contract | `01-runtime-receipt-contracts` | Empty `CapabilityScopeJson`; missing required receipts in QA recheck | Unit tests for contract model/compiler and receipt requirement mapping |
| R2 HR Readiness And Matching | `02-hr-capability-readiness` | Project-structure role matching did not flag proof-readiness gap | Readiness tests and UI/preview evidence for missing MCP/tool/skill cases |
| R3 Runtime Metadata And MAF Boundary | `01-runtime-receipt-contracts`, `04-template-process-e2e` | Existing metadata channel and domain leak concern | Source proof that MAF remains generic and process-owned instructions move to templates/drivers |
| R4 Outcome Receipt Gate | `01-runtime-receipt-contracts` | Artifact-only recheck outcome accepted despite missing receipts | Negative test where success outcome without receipts is rejected or routed |
| R5 Manager Fallback And Process Drivers | `03-manager-fallback-drivers` | Repeated artifact-only retries and finalizer recovery | Tests for fallback decision: redispatch, reassign, driver recovery, or NeedsAttention |
| R6 Template Migration | `04-template-process-e2e` | Software-delivery and screenshot process templates contain prose-only proof rules | Template validation and E2E process proof receipts |
| R7 Testability And Performance | All subbundles | Large mixed responsibilities and runtime catalog cost risk | Focused service tests plus architecture review of lifetimes, caching, and boundaries |
