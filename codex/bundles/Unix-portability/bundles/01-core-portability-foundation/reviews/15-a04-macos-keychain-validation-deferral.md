# A04 macOS Keychain validation deferral

## Operator decision

On 2026-08-09 the operator explicitly deferred genuine macOS Keychain execution so the portability bundles can continue. This changes the timing of evidence, not the implementation or security behavior.

## Scope

- `MacOsKeychainSecretVault` implementation, typed availability/failure behavior, fake-native CRUD/restart/concurrency/access-control contracts, redaction, and fail-closed explicit selection remain required and implemented.
- Genuine macOS create/read/update/delete/restart/concurrency/access-control execution is moved to post-bundle follow-up `MACOS-KEYCHAIN-VALIDATION-001`.
- Until that follow-up passes, documentation and capability surfaces must describe the explicit Keychain provider as implemented but actual-host unverified. They must not claim verified macOS Keychain support.
- The deferral removes actual Keychain execution as a blocker for C2, C4, and runtime-bundle progression. It does not waive any actual-host requirement unrelated to Keychain unless separately amended.

## Re-gate decision

The independent A04 review found no remaining implementation, architecture, migration, redaction, Windows, or Linux blocker. With the sole missing Keychain actual-host proof explicitly deferred, Gate C2 is GO and A05 is eligible.

## Follow-up acceptance

Run `MacOs_keychain_actual_session_crud_and_restart_when_enabled` on a genuine isolated macOS user/Keychain, retain non-disclosing evidence, update support claims, and close `MACOS-KEYCHAIN-VALIDATION-001` before declaring the Keychain profile actual-host verified.
