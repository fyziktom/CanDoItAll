# Exit criteria

- [x] Gate C2 is GO after independent implementation review and explicit operator deferral of unavailable genuine macOS Keychain execution.
- [x] A default local profile starts without an interactive/external vault: Windows reports DPAPI/`Strong`; Unix reports `LocalUserFile`/`BasicLocal` without exposing secrets or paths.
- [x] Auto never selects an unsupported or undeclared tier: Windows selects DPAPI/`Strong`; Unix selects the explicitly documented `BasicLocal` tier.
- [x] Production key material uses the declared platform tier: Windows DPAPI, Unix `BasicLocal` with enforced `0700`/`0600`, or an explicitly configured stronger provider.
- [x] Legacy Windows secret/control-plane data has a tested migration and rollback path.

- [x] Execution report and conditional-stop handoff include the SEC-014 remediation and proof.
- [x] No secret-bearing content exists in artifacts.
