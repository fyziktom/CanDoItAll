# Driver Contract Boundary

## Contract-Only API Candidate
The bundle may add a production abstractions package, but it must be **behavior-free**.

Recommended contract families:
- `ProcessDriverPermissionMode`
- `ProcessDriverCapabilityScope`
- `ProcessDriverEvidenceReference`
- `ProcessDriverAuditFact`
- `ProcessDriverRedactionStatus`
- `ProcessDriverDeniedOperation`
- `ProcessDriverVerificationRequest`
- `ProcessDriverVerificationResponse`
- `ProcessDriverDiagnostic`
- `ProcessDriverContractVersion`

## Explicitly Forbidden Type Families
- `ProcessDriverRegistry`
- `ProcessDriverRuntime`
- `ProcessDriverSelector`
- `ProcessDriverProvider`
- `ProcessDriverHost`
- `ProcessDriverManagerCommand`
- `ProcessDriverServiceCollectionExtensions`
- execution-capable implementations
- connector-backed implementations

## Architecture Test Expectations
- Driver contract project has no package references.
- Driver contract project has no references to Modules, Infrastructure, AgentFramework, EF, UI, or storage.
- Production source contains no registry/runtime/selector/DI/manager command tokens.
- `VerificationOnly` and `ManagerReadonly` cannot express mutation operations.
