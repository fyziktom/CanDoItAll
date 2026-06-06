# Requirements

- **REQ-001**: Do not create CanDoItAll.Processes.Core in this bundle unless an explicit red-team gate proves all blockers are gone; default decision is no Core split.
- **REQ-002**: Preserve all current process dispatch behavior; this is architecture/refactoring only, not feature removal or simplification.
- **REQ-003**: Continue using fewer but broader subbundles: every subbundle must cover a coherent multi-file isolation slice, not a single rename/move.
- **REQ-004**: Advance multiple remaining isolation areas in one bundle: route services, route model adapters, hydration, subprocess runtime, finalizer application, transition/failure closure, process-owned contracts, and driver readiness.
- **REQ-005**: Keep production process-driver APIs out of scope; driver work is documentation/contracts-readiness only.
- **REQ-006**: Keep UI/browser proof N/A unless source changes touch UI. Do not create small/medium/mobile screenshots or viewport proof artifacts.
- **REQ-007**: Maintain route stage order and behavior: FreshRecoverySkip -> DatabaseRequirement -> UpstreamMaterialization -> StrandedArtifactRecovery -> Subprocess -> StartTransition -> Workflow -> DirectAgentExecution -> CompetingExecutionGuard -> RunClosedGuard -> FinalizerTransition.
- **REQ-008**: Reduce adapter-heavy boundaries by moving logic into module-local services and shrinking dispatcher bridge APIs where safe.
- **REQ-009**: Ensure tests prove parity, not just compile: focused unit + focused integration + build + source scans + anti-stub scans + route order scans.
- **REQ-010**: Create a final Core/Driver readiness decision matrix with a concrete go/no-go recommendation for the next bundle.
