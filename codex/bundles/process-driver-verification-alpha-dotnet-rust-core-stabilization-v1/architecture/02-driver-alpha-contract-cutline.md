# Driver Alpha Contract Cutline

## Boundary
The alpha driver may implement verification-only behavior but must not be runtime-integrated.

## Inputs
- `ProcessDriverVerificationRequest`
- transcript/evidence references
- supplied transcript content from tests or future caller adapter
- capability scope and permission mode

## Outputs
- accepted/denied response
- diagnostics
- evidence references
- audit fact(s)
- redaction descriptor
- no-mutation proof

## Out Of Scope
- reading arbitrary repo paths
- resolving bundle URLs
- executing commands
- writing files/artifacts
- calling process runtime
- mutating business records
