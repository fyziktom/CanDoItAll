# Validation matrix

| Subbundle | Entry | Focused proof examples | Minimum closure |
|---|---|---|---|
| A00 | Program entry | python ./scripts/scan_portability.py --repo-root <repo> --output <repo>/artifacts/unix-portability/A00/portability-scan.json; dotnet restore ./CanDoItAll.slnx | Gate C0 is GO with an exact current commit.; No unclassified P0/P1 finding or unknown persisted path/key record remains. |
| A01 | A00 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~Path\|FullyQualifiedName~Workspace\|FullyQualifiedName~Storage'; dotnet build ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release | All path categories and ownership boundaries are documented and tested.; New logical path writers are host-independent and legacy readers are field-scoped. |
| A02 | A01 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~FileSystem\|FullyQualifiedName~Storage\|FullyQualifiedName~Symlink\|FullyQualifiedName~Permission\|FullyQualifiedName~Watcher'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=FileSystemPortability' | Gate C1 is GO after independent architecture/security review.; Filesystem semantics are deterministic and actual-host tested. |
| A03 | A02, Gate C1 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~Storage\|FullyQualifiedName~ControlPlane\|FullyQualifiedName~DatabaseProfile\|FullyQualifiedName~FileApplicationPreference'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=StorageMigration' | Every path-bearing persisted record is logical or explicitly host-bound/versioned.; Old Windows locators and profiles have proven migration/rebind/rollback. |
| A04 | A03 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~Secret\|FullyQualifiedName~Vault\|FullyQualifiedName~DataProtection\|FullyQualifiedName~DatabaseProfile'; dotnet test ./tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj -c Release --filter 'Category=SecretPortability\|Category=SecretMigration' | Gate C2 is GO from architect, security reviewer, and runtime validator.; Auto never selects unsupported or insecure persistence. |
| A05 | A04, Gate C2 | dotnet test ./tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj -c Release --filter 'FullyQualifiedName~Composition\|FullyQualifiedName~Capability\|FullyQualifiedName~Readiness\|FullyQualifiedName~Architecture'; dotnet build ./CanDoItAll.slnx -c Release --no-restore /m:1 | Mandatory providers are selected truthfully and optional capabilities degrade independently.; No giant platform abstraction or process-semantic leakage was introduced. |
| A06 | A05 | dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r linux-x64 --self-contained false -o <artifact>/linux-x64; dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r osx-arm64 --self-contained false -o <artifact>/osx-arm64 | Clean headless startup/restart succeeds on Windows, Ubuntu, and macOS.; Publish/support claims are bounded to proven RIDs and profiles. |
| A07 | A06 | dotnet restore ./CanDoItAll.slnx; dotnet build ./CanDoItAll.slnx -c Release --no-restore /m:1 | Core Gate C4 is GO on an exact commit with active Windows/Ubuntu/macOS evidence.; All core P0 requirements are Solved and no critical finding remains open. |

## Universal gates

Every mandatory subbundle also runs the repository stable Release gate appropriate to its point in the program and records:

- exact command and exit code;
- OS/profile/architecture/tool versions;
- log/evidence path;
- pre-existing versus introduced failures;
- redaction result;
- changed requirements/findings/source references.

By the final bundle gate, Windows, Ubuntu, and macOS actual-host evidence is mandatory.
