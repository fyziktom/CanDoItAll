# Nonfunctional constraints

## Security

- Fail closed on unsupported secret/key provider, insecure mode, root escape, foreign executable path, or ambiguous migration.
- No secret values in logs, exceptions, receipts, reports, screenshots, backups, or scanner excerpts.
- No automatic privilege escalation.
- Preserve approvals, workspace authority, TLS, and tool policy.

## Compatibility

- Existing Windows data remains readable or has a transactional Windows-side migration.
- Existing Windows behavior remains green.
- Old logical separators and path aliases have bounded compatibility readers.
- No silent destructive migration or forced data relocation.

## Reliability

- Writes are atomic where promised and cross-process safe.
- Restart and interrupted migration are first-class tests.
- Watchers converge through rescan.
- Support claims name actual OS/profile/RID/dependency evidence.

## Architecture

- No broad platform god service.
- No reverse MAF/process semantic dependency.
- No duplicated path or secret stack.
- Common code uses portable .NET APIs directly whenever possible.

## Maintainability

- Source-code comments are English.
- New contracts have one owner and clear dependency direction.
- Every migration format is versioned and documented.
- Conditional recovery paths remain executable.
