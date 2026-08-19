# Headless Web Host Operations

This runbook covers framework-dependent CanDoItAll Web deployment without desktop, terminal, Manager, MCP, or local-tool requirements. The application still serves its normal Web UI; "headless" means the host process has no desktop-session dependency.

Start with [Installing instances](installing-instances.md) for deployment-model selection,
common prerequisites, and platform default paths. This runbook contains the detailed
service lifecycle.

## Support contract

Every publish contains `runtime-support.json`. Treat it as the artifact's bounded claim, not as proof that an untested host works.

| RID | Publish contract | Actual runtime evidence |
| --- | --- | --- |
| `win-x64` | Framework-dependent | Validated on Windows x64 |
| `linux-x64` | Framework-dependent | Validated on Ubuntu x64 |
| `osx-x64` | Framework-dependent | Embedded support manifest reports `ActualHostUnverified` |
| `osx-arm64` | Framework-dependent | Embedded support manifest reports `ActualHostUnverified` |

The host requires the matching .NET 10 ASP.NET Core runtime, PostgreSQL 16, writable purpose roots, and a stable `CANDOITALL_HOST_BINDING_ID`. Production Unix hosts also require an explicitly configured certificate-backed ASP.NET Core Data Protection key protector. A cross-RID publish is not actual-host evidence. macOS Keychain execution requires a genuine interactive user Keychain; headless `Auto` uses LocalUserFile/`BasicLocal`, not Keychain.

## Publish

Publish outside the repository. Installation artifacts use the sibling Components and
FileTools source graph, so keep `UseLocalCanDoItAllLibraries=true` for both restore and
publish and record both dependency commits. Restore separately for each target RID. Do
not add self-contained, trimming, single-file, or Native AOT properties to these commands:

```text
dotnet restore ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -r linux-x64 -p:UseLocalCanDoItAllLibraries=true
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r linux-x64 --self-contained false --no-restore -o /absolute/artifacts/linux-x64 -p:UseLocalCanDoItAllLibraries=true

dotnet restore ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -r osx-x64 -p:UseLocalCanDoItAllLibraries=true
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r osx-x64 --self-contained false --no-restore -o /absolute/artifacts/osx-x64 -p:UseLocalCanDoItAllLibraries=true

dotnet restore ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -r osx-arm64 -p:UseLocalCanDoItAllLibraries=true
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj -c Release -r osx-arm64 --self-contained false --no-restore -o /absolute/artifacts/osx-arm64 -p:UseLocalCanDoItAllLibraries=true
```

On Windows use an absolute directory outside the checkout and `-r win-x64` on both
commands. Verify that the output contains `CanDoItAll.Web.dll`, `runtime-support.json`,
`Templates`, and the static Web assets before installation.

## Linux systemd profile

Create a dedicated, non-login user and owned roots with the operating-system administration mechanism approved for the host. The repository scripts never elevate privileges.

| Purpose | Suggested path | Mode |
| --- | --- | --- |
| Releases and launcher | `/opt/candoitall` | `0750`, service user/group |
| Workspace/data | `/var/lib/candoitall/data` | `0750` |
| Control plane/config | `/var/lib/candoitall/config` | `0750` |
| State | `/var/lib/candoitall/state` | `0750` |
| Logs | `/var/log/candoitall` | `0750` |
| Runtime temporary | `/run/candoitall` | `0750`, recreated for the service |
| Environment file | `/etc/candoitall/candoitall.env` | `0640`, root/service group |

The environment file should contain non-secret deployment facts:

```text
RuntimeHost__Profile=LinuxHeadless
SecretVault__UsageProfile=Headless
SecretVault__Provider=Auto
CANDOITALL_HOST_BINDING_ID=linux-service-host-001
XDG_DATA_HOME=/var/lib/candoitall/data
XDG_CONFIG_HOME=/var/lib/candoitall/config
XDG_STATE_HOME=/var/lib/candoitall/state
XDG_RUNTIME_DIR=/run/candoitall
ControlPlane__LogsRootPath=/var/log/candoitall
DataProtection__KeyProtection__Provider=Certificate
DataProtection__KeyProtection__CertificatePath=/etc/candoitall/dataprotection.pfx
DataProtection__KeyProtection__CertificatePasswordEnvironmentVariable=CANDOITALL_DP_CERTIFICATE_PASSWORD
Database__Provider=PostgreSql
Database__ConnectionString=Host=/var/run/postgresql;Database=candoitall;Username=candoitall
FileTools__DesktopLaunch__Enabled=false
```

The password-free connection example assumes PostgreSQL peer authentication maps the service account to a dedicated non-superuser database role. Supply `CANDOITALL_DP_CERTIFICATE_PASSWORD` through the host's approved secret injection or the permission-restricted environment file. For a remote database, inject the connection string through the same approved mechanism; never put either secret in the unit, repository, command history, or captured diagnostics. The PFX must contain its private key and must grant no group or other access. Use `ExternalWrappingKeyFile` when same-user access to the BasicLocal vault is in scope.

Install an immutable release as the service user:

```text
tools/install/unix/install-candoitall-web.sh --artifact /absolute/artifacts/linux-x64 --install-root /opt/candoitall --release-id 2026.08.10-1
```

Render `tools/install/unix/candoitall-web.service.in` by replacing every `@@...@@` token with the reviewed service user, group, install root, environment file, port, and writable roots. Install the rendered unit through the host's normal change-control path, then run:

```text
systemctl daemon-reload
systemctl enable --now candoitall-web.service
systemctl status candoitall-web.service
curl --fail http://127.0.0.1:5032/health
curl --fail http://127.0.0.1:5032/api/runtime/operations
```

Use `journalctl -u candoitall-web.service` for bounded service diagnostics. The operations endpoint reports typed platform/profile, provider/capability, path-readiness, publication, and validation state without full roots, connection strings, or secret values.

## macOS launchd profile

For an interactive user, use `~/Library/Application Support/CanDoItAll` and `~/Library/Logs/CanDoItAll` and run the launcher in that user's session. The supplied plist is intentionally a system LaunchDaemon template, not a per-user LaunchAgent. For the daemon, create a dedicated service account and owned directories below `/Library/Application Support/CanDoItAll` and `/Library/Logs/CanDoItAll`; do not reuse another user's home or Keychain.

Install the matching framework-dependent artifact with the same Unix installer. Render `tools/install/unix/com.candoitall.web.plist.in` with the dedicated service user, service group, install root, port, stable host-binding ID, and log root. The `UserName` and `GroupName` tokens are mandatory; reject the rendered file if any token remains. Add non-secret purpose-root and PostgreSQL configuration to the plist's `EnvironmentVariables` dictionary or to the account's managed launch configuration. Do not place database passwords or wrapping keys in the plist.

Install the rendered daemon under `/Library/LaunchDaemons` through the host's approved administration path. Do not install this template under `~/Library/LaunchAgents`; a user agent inherits its logged-in account and requires a separately reviewed template without `UserName` or `GroupName`. Validate and load the supplied daemon in the system domain:

```text
plutil -lint com.candoitall.web.plist
launchctl bootstrap system com.candoitall.web.plist
launchctl kickstart -k system/com.candoitall.web
curl --fail http://127.0.0.1:5032/health
curl --fail http://127.0.0.1:5032/api/runtime/operations
```

The Keychain interactive profile requires a genuine available user Keychain and is not a headless substitute. macOS artifacts and profiles must retain the embedded manifest's `ActualHostUnverified` label until the manifest and accepted actual-host evidence are deliberately updated together.

## Upgrade, restart, and rollback

1. Create and restore-test a PostgreSQL backup outside the install root.
2. Preserve the control-plane, workspace, vault/key, state, and database roots. Releases are replaceable; those roots are authoritative.
3. Install a unique new release ID. The installer records the old active release as `previous-release` and atomically changes `active-release`.
4. Restart the service and verify `/health` and `/api/runtime/operations`.
5. If startup fails before a data/schema migration may have committed, run `rollback-candoitall-web.sh INSTALL_ROOT`, restart, and verify health.
6. If a migration may have committed, do not blindly switch binaries. Follow the migration's documented forward-repair or database-restore path.

The rollback command is idempotent and never deletes a release or data root. Keep the previous release until backup restoration and the new version have both been rehearsed. Stop with `SIGINT` and allow the configured 30-second service timeout before escalation.

## Troubleshooting and limitations

- A failed mandatory root, database, migration, or selected strong vault blocks startup by design. Fix the reported typed reason; do not select a weaker provider implicitly.
- `LocalUserFile` is encrypted at rest and hardened to the service user, but remains `BasicLocal` because same-user processes can access its colocated key.
- Desktop open/reveal, interactive terminal, native process discovery, Manager, MCP, and local tools are optional or outside the headless-core support claim.
- Preserve the stable host-binding ID across normal restarts/upgrades. Changing it intentionally requires explicit rebind of host-bound paths.
- Never attach complete environment dumps, connection strings, physical root listings, vault files, or Keychain/keyring output to support evidence.
