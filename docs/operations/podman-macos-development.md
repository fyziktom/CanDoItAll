# Podman on macOS: source development

Run PostgreSQL in Podman and the Blazor host directly on macOS. This is a local
development workflow, not an installed-instance repair procedure. The commands were
reviewed against repository configuration; macOS execution has not been validated in
this integration run. Retain the platform status in [Installing instances](installing-instances.md).

## Prerequisites and source layout

Install the SDK selected by [global.json](../../global.json), Node/npm for application
Tailwind generation, and Podman using the [official installation guide](https://podman.io/docs/installation).
Install a compatible external Compose provider and verify `podman compose version`.
The [Podman Compose command](https://docs.podman.io/en/stable/markdown/podman-compose.1.html)
delegates to that provider; behavior and supported options depend on its version.

Default source mode requires all three sibling repositories, even when only the database
runs in a container:

```text
workspace/
  CanDoItAll/
  CanDoItAll.Components/
  CanDoItAll.FileTools/
```

Use the reviewed sibling commits pinned in [.github/workflows/ci.yml](../../.github/workflows/ci.yml).
Components must contain both BaseLib `css/material-symbols.css` and committed
`css/output.css`. Do not compensate for missing source assets by adding Node to the
application Dockerfile. See [build dependency modes](../../README.md#build-dependency-modes).

## Machine and database

Create a dedicated machine once; reuse it on later sessions. Check the installed
[machine-init options](https://docs.podman.io/en/stable/markdown/podman-machine-init.1.html)
before choosing a provider. The explicit Apple Hypervisor example is:

```sh
podman machine init --provider applehv podman-machine-dev
podman machine start podman-machine-dev
podman system connection default podman-machine-dev
podman machine list
podman system connection list
```

No extra Homebrew tap or trust/untrust operation is required by this guide. Do not remove
an existing machine or change its provider merely to try this workflow.

From the CanDoItAll root, create these files only if they do not already exist; preserve
any existing configuration and secrets:

```sh
cp -n .env.example .env
mkdir -p .secrets
test -e .secrets/db-password || printf '%s' 'candoitall' > .secrets/db-password
chmod 600 .secrets/db-password
cp -n compose.override.yaml.example compose.override.yaml
```

`candoitall` is a disposable development-only example matching the committed
`Database:ConnectionString` in appsettings.Development.json. Never reuse it on a shared
host. Existing PostgreSQL volumes keep their original password; changing the secret file
does not rotate a database password. See [Container operations](containers.md#configuration).
If your database differs, supply `Database__ConnectionString` through approved private
configuration; do not paste credentials into logs or version-controlled files.

The override makes the containerized app opt-in and publishes PostgreSQL on
`127.0.0.1:5432` by default. If your Podman provider rejects the base model's `local`
logging driver, merge this service override into the ignored file without discarding its
existing ports/profile settings:

```yaml
services:
  db:
    logging:
      driver: json-file
      options:
        max-size: "10m"
        max-file: "3"
```

```sh
podman compose --env-file .env config --quiet
podman compose up -d --wait db
podman compose ps
```

Confirm the selected Compose provider supports `--wait`; do not treat a running process
as a ready database when it does not.

## Run and validate the application

```sh
npm ci --prefix Tailwind
npm run tailwind:build
dotnet restore CanDoItAll.slnx -p:UseLocalCanDoItAllLibraries=true
dotnet build CanDoItAll.slnx --no-restore -p:UseLocalCanDoItAllLibraries=true
npm run watch
```

Open the address printed by the host. Verify `/health` and `/api/runtime/operations`,
then check that icons and both BaseLib stylesheets load. Keep the same dependency mode
for restore, build, test and run.

Package mode is an explicit alternative, not an automatic fallback when siblings are
missing. Use `-p:UseLocalCanDoItAllLibraries=false` consistently in a separate clean
checkout/output graph. Until coordinated packages are published, use a temporary local
NuGet configuration and the exact locally packed versions; never add that feed to the
global or committed NuGet configuration. The integration proof feed contains ten
Components and nine FileTools nupkg files at 0.3.0 beneath
`.artifacts/ui-refactoring-integration/local-feed-0.3.0`; see the execution report for
hashes. Use a separate package cache to avoid an older local package with the same version.

## Optional full container stack

The current Dockerfile requires named sibling contexts and support for its Dockerfile
syntax, including `COPY --exclude`. Verify your engine supports that contract. If it
rejects the syntax, use a compatible BuildKit engine; do not remove the host-output
exclusions or claim a successful Podman build.

```sh
podman build \
  --build-context components=../CanDoItAll.Components \
  --build-context filetools=../CanDoItAll.FileTools \
  --file src/App/CanDoItAll.Web/Dockerfile \
  --tag candoitall-app:dev .
podman compose --profile container-app up -d --no-build --wait
```

The `container-app` profile is needed while the direct-host override remains present.
This is still source mode, not package-mode restore. See [Container operations](containers.md).

## Shutdown and destructive recovery

Normal shutdown preserves data:

```sh
podman compose down
podman machine stop podman-machine-dev
```

For a failed database container, inspect `podman compose ps --all` and its logs before
using `podman compose rm -sf db` and starting `db` again. This removes that container,
not its named volume. Back up required data first.

**Destructive, disposable data only:** `podman compose down --volumes` permanently
removes this Compose project's database and application data. Inspect the project name
and back up before using it. `podman machine rm podman-machine-dev` removes every
resource in that dedicated VM, including resources from any other projects placed there.
Do not use global prune commands as routine troubleshooting. See
[Backup and restore](backup-and-restore.md) before any data removal.
