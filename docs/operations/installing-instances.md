# Installing Instances

This guide is the platform entry point for building and installing CanDoItAll. It
separates source development, the development Compose stack, the dedicated Windows app,
and framework-dependent Web-host deployments because they have different state and
lifecycle owners.

## Choose A Deployment Model

| Model | Hosts | Dependency graph | State owner | Intended use |
| --- | --- | --- | --- | --- |
| Direct source run | Windows, Linux, macOS | Sibling source repositories by default | Current operating-system user | Development and interactive local use |
| Development Compose stack | Any host with a Linux Compose engine | Pinned NuGet packages inside the image build | Compose volumes `app-data` and `db-data` | Disposable or persistent local development |
| Dedicated Windows app | Windows x64 | Installer-published application | Per-user install root and installer-managed PostgreSQL | Installed local Windows instance |
| Framework-dependent Web host | Windows x64, Linux x64, macOS x64/arm64 | Use the pinned NuGet graph for reproducible artifacts | Operator-selected release, data, configuration, state, log, and database roots | Long-running headless Web service |

The Web UI is available in every model. "Headless" means the server process does not
depend on a desktop session; it does not mean the application has no UI.

## Common Build And Runtime Contract

Source builds require the SDK selected by [`global.json`](../../global.json). The default
development graph expects `CanDoItAll`, `CanDoItAll.Components`, and
`CanDoItAll.FileTools` as sibling repositories. See the root
[build dependency modes](../../README.md#build-dependency-modes) section for custom roots
and package-mode commands.

Reproducible installation artifacts must use package mode throughout restore and publish:

```text
dotnet restore ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --runtime <RID> -p:UseLocalCanDoItAllLibraries=false
dotnet publish ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release --runtime <RID> --self-contained false --no-restore --output <ABSOLUTE_OUTPUT> -p:UseLocalCanDoItAllLibraries=false
```

Framework-dependent artifacts require the matching .NET 10 ASP.NET Core runtime on the
target host. Do not add self-contained, trimming, single-file, or Native AOT properties to
that deployment contract. Verify that the artifact contains `CanDoItAll.Web.dll`,
`runtime-support.json`, `Templates`, and the static Web assets.

Every non-development instance also requires:

- PostgreSQL 16 with a dedicated non-superuser application role;
- owned, writable workspace and control-plane purpose roots;
- a stable `CANDOITALL_HOST_BINDING_ID` containing 8-128 ASCII letters, digits, hyphens,
  or underscores;
- an explicit production secret and Data Protection configuration;
- authorization and HTTPS at the application or trusted reverse-proxy boundary before
  exposure beyond loopback.

After startup, verify both endpoints:

```text
curl --fail http://127.0.0.1:5032/health
curl --fail http://127.0.0.1:5032/api/runtime/operations
```

The operations endpoint reports typed platform, host-profile, capability, path-readiness,
and deployment-support state without returning secret values or full physical roots.

## Default User-Owned Runtime Roots

Direct source runs use platform defaults outside the repository. Service and container
deployments should set explicit roots instead of relying on a service account's home.

| Purpose | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Workspace | `%LOCALAPPDATA%\CanDoItAll\workspace` | `$XDG_DATA_HOME/candoitall/workspace`, otherwise `~/.local/share/candoitall/workspace` | `~/Library/Application Support/CanDoItAll/workspace` |
| Control plane | `%LOCALAPPDATA%\CanDoItAll\control-plane` | `$XDG_CONFIG_HOME/candoitall/control-plane`, otherwise `~/.config/candoitall/control-plane` | `~/Library/Application Support/CanDoItAll/control-plane` |
| Data Protection keys | `control-plane\dataprotection-keys` | `$XDG_DATA_HOME/candoitall/dataprotection-keys` | `~/Library/Application Support/CanDoItAll/dataprotection-keys` |
| State | `%LOCALAPPDATA%\CanDoItAll\state` | `$XDG_STATE_HOME/candoitall`, otherwise `~/.local/state/candoitall` | `~/Library/Application Support/CanDoItAll/state` |
| Logs | `%LOCALAPPDATA%\CanDoItAll\logs` | State root plus `/logs` | `~/Library/Logs/CanDoItAll` |
| Runtime temporary data | `%TEMP%\CanDoItAll\runtime` | `$XDG_RUNTIME_DIR/candoitall`, otherwise `$TMPDIR/candoitall-runtime` | `$TMPDIR/CanDoItAll/runtime` |

Override these roots with `Storage__WorkspaceRoot`, `ControlPlane__RootPath`,
`ControlPlane__DataProtectionKeysPath`, `ControlPlane__StateRootPath`,
`ControlPlane__LogsRootPath`, and `ControlPlane__RuntimeTemporaryRootPath`. A service or
container must retain the same host-binding ID and authoritative roots across upgrades.
Moving a database or filesystem catalog to another host does not make its physical paths
portable; rebind them explicitly after verifying the new roots.

See [Storage, paths, and host portability](../architecture/storage-and-path-portability.md)
for the binding and driver model, and [Secure configuration](../secure-configuration.md)
for vault and Data Protection choices.

## Windows

### Direct Development

Use the sibling-source layout from the root README, start PostgreSQL, and run:

```powershell
docker compose up -d --wait db
dotnet run --project ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj
```

The automatic interactive profile uses Windows user roots from the table above and DPAPI
for current-user secret protection.

### Dedicated Per-User App

The canonical Windows installer publishes a self-contained app, creates shortcuts, and
prepares a dedicated PostgreSQL backend:

```powershell
./tools/install/Install-CanDoItAllWebApp.ps1 -StartAfterInstall
```

Its default root is `%LOCALAPPDATA%\CanDoItAll\WebApp`. Runtime database metadata,
protected credentials, native binaries when selected, and native database data live
beneath `runtime\database`. A working Linux Docker engine selects one separately managed
container and named volume; otherwise the installer creates a per-user native PostgreSQL
cluster. Neither backend belongs to the repository Compose project.

Use [`installed-web-app.md`](installed-web-app.md) for engine selection, exact managed
paths, repair, backup, and restore. The database-only repair entry point is:

```powershell
./tools/install/Install-CanDoItAllWebAppDatabase.ps1
```

### Framework-Dependent Windows Host

Publish with RID `win-x64` and configure `RuntimeHost__Profile=WindowsHeadless` when the
process runs without an interactive user. The repository does not install a Windows
service for this model; the operator owns service registration, explicit writable roots,
database configuration, restart policy, and backup. Follow the common contract and the
[headless Web host runbook](headless-web-host.md).

## Linux

Publish with RID `linux-x64`. For a long-running service, use a dedicated non-login user
and explicit roots; the reviewed profile uses `/opt/candoitall` for immutable releases,
`/var/lib/candoitall` for data/configuration/state, `/var/log/candoitall` for logs,
`/run/candoitall` for temporary runtime data, and
`/etc/candoitall/candoitall.env` for permission-restricted configuration.

Set at least:

```text
RuntimeHost__Profile=LinuxHeadless
SecretVault__UsageProfile=Headless
CANDOITALL_HOST_BINDING_ID=linux-service-host-001
XDG_DATA_HOME=/var/lib/candoitall/data
XDG_CONFIG_HOME=/var/lib/candoitall/config
XDG_STATE_HOME=/var/lib/candoitall/state
XDG_RUNTIME_DIR=/run/candoitall
ControlPlane__LogsRootPath=/var/log/candoitall
Database__Provider=PostgreSql
FileTools__DesktopLaunch__Enabled=false
```

Production Unix hosts must explicitly configure certificate-backed ASP.NET Core Data
Protection. Supply database and certificate secrets through the host's approved secret
injection; do not put them in the unit file, repository, shell history, or captured
diagnostics.

Install a published artifact as the service user:

```sh
./tools/install/unix/install-candoitall-web.sh \
  --artifact /absolute/artifacts/linux-x64 \
  --install-root /opt/candoitall \
  --release-id 2026.08.14-1
```

Render and install `tools/install/unix/candoitall-web.service.in`; the script does not
elevate privileges or install the systemd unit. The complete hardening, environment,
service, restart, and rollback procedure is in
[`headless-web-host.md`](headless-web-host.md#linux-systemd-profile).

The baseline `LinuxHeadless` profile is actual-host validated. Its automatic local-user
vault is reported as `BasicLocal`; use an explicitly configured stronger provider where
same-user access is not acceptable.

## macOS

Use RID `osx-arm64` on Apple silicon and `osx-x64` on Intel. Interactive runs use the
Application Support, Logs, and temporary roots in the default-root table. A system
LaunchDaemon must instead use a dedicated service account and explicit owned roots below
`/Library/Application Support/CanDoItAll` and `/Library/Logs/CanDoItAll`; it must not
reuse another user's home or Keychain.

Install the matching artifact with the same immutable Unix installer:

```sh
./tools/install/unix/install-candoitall-web.sh \
  --artifact /absolute/artifacts/osx-arm64 \
  --install-root '/Library/Application Support/CanDoItAll/app' \
  --release-id 2026.08.14-1
```

Set `RuntimeHost__Profile=MacOsHeadless`, `SecretVault__UsageProfile=Headless`, a stable
host-binding ID, explicit purpose roots, PostgreSQL configuration, and
`FileTools__DesktopLaunch__Enabled=false`. Render
`tools/install/unix/com.candoitall.web.plist.in`, reject any remaining `@@...@@` token,
and install it as a system LaunchDaemon. The supplied plist is not a per-user LaunchAgent
template.

The embedded deployment-support manifest currently marks both macOS publish targets and
the `MacOsHeadless` profile as `ActualHostUnverified`. Build and publish support is
present, but production claims must retain that evidence status until the manifest and
its actual-host proof are deliberately updated together. Keychain use is an interactive
user capability, not a headless daemon fallback; production headless deployments require
explicit certificate-backed Data Protection.

See [`headless-web-host.md`](headless-web-host.md#macos-launchd-profile) for the complete
launchd, validation, restart, and rollback procedure.

## Development Compose Stack

The repository Compose model always runs the Linux Web image, even when Docker Desktop is
hosted on Windows or macOS. It uses package-mode restore and owns `app-data` and `db-data`
named volumes. Application state is under `/data`; PostgreSQL state is under
`/var/lib/postgresql/data` in the database volume.

Normal teardown preserves both volumes:

```text
docker compose down
```

Use [Container operations](containers.md) and
[Development PostgreSQL backup and restore](backup-and-restore.md) for lifecycle and
recovery. Do not use Compose commands to manage the dedicated Windows app database.
