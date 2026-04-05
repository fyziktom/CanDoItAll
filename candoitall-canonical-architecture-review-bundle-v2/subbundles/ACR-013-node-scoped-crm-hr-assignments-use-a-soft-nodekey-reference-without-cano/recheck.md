# Prospective re-check

## Expected result after remediation

Prospective pass if node-scoped assignments cannot be saved unless the target node exists in the same project and the role is allowed for that node kind.

## QA focus

Soft strings are acceptable only for projection identifiers, not for canonical mutation scopes.

## Back-check question

Would the same skill lens still flag this concern after the remediation?

Expected answer: **No**, unless a cache/projection/UI layer still owns live truth by accident.
