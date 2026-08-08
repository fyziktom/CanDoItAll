# Assumptions and risks

## Working assumptions

- Windows behavior and existing Windows data are compatibility requirements.
- PostgreSQL remains the production database baseline.
- The first portable support claim is a headless Web host.
- Interactive Keychain/Secret Service and desktop actions are separate optional profiles.
- The execution agent has access to real Windows, Ubuntu, and macOS runners or machines before claiming support.

## Critical path risks

1. A broad slash conversion corrupts physical paths, URLs, or Unix filenames.
2. Cross-OS profile import silently reinterprets foreign absolute paths.
3. Data Protection/key/vault bootstrap becomes circular.
4. Legacy DPAPI or Data Protection payloads become unreadable after provider/key-ring changes.
5. macOS filesystem case behavior is inferred only from OS name.
6. atomicity is tested only in-process while multiple application instances remain possible.
7. Unix file modes are assumed from umask rather than verified.
8. an optional desktop/keyring dependency blocks headless startup.
9. CI provides compile-only evidence and is presented as runtime support.

## Validation risks

- The preparation environment did not have a local checkout, so all local build/test facts remain pending A00.
- GitHub-hosted macOS hardware/architecture can differ from user machines; record actual architecture and bound claims.
- A test vault or in-memory provider in CI can hide production bootstrap defects; production-profile tests remain required.
- Filesystem tests on one volume do not prove every case mode; the root policy must handle uncertainty explicitly.

## Reopen triggers

Reopen the owning foundation when:

- a new persisted path/secret/key record is discovered;
- the source anchor or ownership graph changes;
- a supported profile needs an insecure fallback;
- an old fixture becomes unreadable;
- a symlink/case/atomicity/mode test contradicts the contract;
- a later runtime change bypasses the core path/storage/secret owner.
