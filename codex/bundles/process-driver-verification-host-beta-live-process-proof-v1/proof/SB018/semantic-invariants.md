# SB018 Semantic Invariants

## Gate F Invariants
- Verification host options must be a real typed model with validation, not only documented configuration keys.
- Full Processes module registration must bind `Processes:VerificationRuntimeHost`.
- The host helper used by DI/tests must register default options and validation without requiring an ambient `IConfiguration`.
- Emergency disable must deny before lane selection, payload construction, orchestration, or success audit append.
- Disabled lanes must deny the requested lane exactly and must not route to another lane.
- Payload item limits apply to the selected lane payload collection.
- Supplied evidence content byte limits apply to selected-lane material that carries supplied content, including transcript text and supplied evidence envelopes.
- Option denials must remain structured, mutation-free, and audited with a denial count.
- P06 must not add fallback selection, reflection/discovery dispatch, generic object payload dispatch, live-provider coupling, raw secret logging, or process-state mutation authority.

## Shallow-Pass Rejections
- Reject a proof package that adds option properties but never reads them in `ProcessVerificationRuntimeHost`.
- Reject a proof package that disables lanes by falling back to another lane.
- Reject a proof package that only validates item count and ignores supplied evidence content size.
- Reject a proof package that requires a full `IConfiguration` to resolve default host services in tests.
- Reject a proof package that omits focused tests for invalid option validation, host disable, lane disable, item limit, and content byte limit.

## Positive Proof Shape
- `ProcessVerificationRuntimeHostOptions` defines `Enabled`, typed `Lanes`, `MaxPayloadItemsPerLane`, and `MaxSuppliedEvidenceContentBytes`.
- `AddProcessesModule` binds `Processes:VerificationRuntimeHost`, while `AddProcessVerificationRuntimeHost` validates defaults without requiring configuration.
- `VerifyAsync` returns `HostDisabled`, `LaneDisabled`, `PayloadLimitExceeded`, or `SuppliedEvidenceContentLimitExceeded` denials before orchestration when policy fails.
- Focused integration tests pass 21 `ProcessDomainEvidenceReadOnlyAdapterTests` and assert each options-policy denial.

## Gate Result
Gate F is semantically adequate for P06. Options are validated and enforced before verification work, with exact lane behavior and bounded payload processing.
