# Service Accounts Authentication Refactor Plan

## Why this is needed
The app currently supports interactive cookie login only. Service accounts (`Bot`, `AIAgent`) need deterministic non-interactive authentication for reliable automation.

## Proposed target
- Add machine-to-machine authentication for API automation:
  - Option A: API key with scoped permissions.
  - Option B: OAuth2 client credentials / JWT bearer.
- Explicitly bypass UI 2FA enforcement for the machine-auth path only.
- Keep interactive user accounts under 2FA policy.

## Required changes
1. Add auth handler and credential storage/rotation model.
2. Bind service principal identity to permissions/roles.
3. Add audit events for credential creation/rotation/revocation.
4. Extend docs and integration tests for machine auth.

## Security constraints
- No shared human credentials for automation.
- Short-lived credentials or rotated API keys.
- Fine-grained scope checking at API policy level.
