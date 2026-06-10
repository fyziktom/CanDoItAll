# Runtime Host Approval Plan

## Phase 1: Verification-only host alpha
Approved in this bundle if gates pass. Supports only supplied-evidence verification lanes and manager-readonly diagnostics.

## Phase 2: Scheduled verification jobs
Not approved in this bundle except as docs/test-only readiness. Requires persisted audit, throttling, operator enablement, and no-mutation proof.

## Phase 3: Workflow verification step
Not approved in this bundle except as docs/test-only readiness. Requires workflow lifecycle ownership and no transition/finalizer mutation.

## Phase 4: Execution-capable drivers
Not approved. Requires sandbox, allowlist, approval, authorization, audit persistence, emergency stop, observability, compatibility/versioning, and red-team proof.
