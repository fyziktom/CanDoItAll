# Next Stage Decision Gates

## If This Bundle Passes
The next bundle may implement the first production **verification-only** driver alpha only if:
- contract-only abstractions exist and are dependency-clean
- permission/denial/audit/redaction tests pass
- `.NET/Rust` rehearsal remains readonly
- no runtime registry/selector/DI/manager command exists
- red-team explicitly approves an alpha boundary

## If This Bundle Fails
Do not implement any driver. Repair:
- permission model
- audit redaction
- contract dependency direction
- architecture tests
- source scans

## Long-Term Driver Release Gates
1. Contract-only API.
2. Verification-only alpha.
3. One module-local adapter with no runtime selector.
4. Read-only domain driver pack after proving audit and denial behavior.
5. Runtime selector only after registry design, policy enforcement, and observability.
6. Execution-capable mode only after sandbox/allowlist/timeout/output-hash/secret-masking proof.
