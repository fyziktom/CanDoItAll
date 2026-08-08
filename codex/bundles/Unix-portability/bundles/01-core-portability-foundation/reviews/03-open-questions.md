# Core open questions to resolve during execution

These are decision gates, not reasons to delay A00.

1. Which existing neutral project should own the pure logical-path contract without polluting SharedKernel or reversing MAF/Infrastructure dependencies?
2. How will root/volume case semantics be determined on macOS and read-only roots?
3. Which headless secure provider best fits local-first deployment: certificate, remote vault, or externally supplied wrapping key?
4. How will ASP.NET Data Protection key-ring at-rest protection integrate with each profile without circular bootstrap?
5. Do real existing users have DataProtectionFileVault data requiring migration, or can that mode be explicitly discarded as alpha/development data?
6. Which application data/config/state root split best matches current installer/service expectations?
7. Is PostgreSQL actual-host CI on macOS installed locally, provided remotely, or separated into a database-independent startup plus scheduled integration?
