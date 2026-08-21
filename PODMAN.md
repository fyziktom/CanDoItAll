# Podman on macOS

Run PostgreSQL in Podman and the CanDoItAll web host directly on macOS. This
avoids building the containerized `app` service and does not require the sibling
`CanDoItAll.Components` or `CanDoItAll.FileTools` repositories.

Run the sections below in order from the repository root.

## 1. Install

Install Podman and the Apple Hypervisor runtime used by the recommended Podman
machine provider:

```sh
brew install podman
brew tap slp/krun
brew trust slp/krun
brew install krunkit
```

## 2. Create and start the Podman machine

Create the named Apple Hypervisor machine once. The explicit name is important:
use it in later `podman machine start` commands.

```sh
podman machine init --provider applehv podman-machine-dev
podman machine start podman-machine-dev
podman system connection default podman-machine-dev
```

On subsequent sessions, start and select the same machine:

```sh
podman machine start podman-machine-dev
podman system connection default podman-machine-dev
```

The rootless-mode warning is expected; this project only publishes ports above
1024. The `podman-mac-helper` warning is also safe to ignore for this workflow:
it is needed only when a tool must use the default Docker API socket. `podman
compose` works without it.

## 3. Set up the local database configuration

Create the development-only database password file. `.secrets/` is ignored by
Git. The `candoitall` value matches the repository's direct-host development
configuration, so no connection-string environment override is needed:

```sh
mkdir -p .secrets
echo 'candoitall' > .secrets/db-password
chmod 600 .secrets/db-password
```

Create the complete ignored Compose override in one command. It exposes
PostgreSQL on the Mac and replaces Docker's unsupported `local` log driver with
Podman's compatible `json-file` driver:

```sh
printf '%s\n' \
  '# Local Podman override: run the web host on macOS and PostgreSQL in Podman.' \
  'services:' \
  '  app:' \
  '    profiles:' \
  '      - container-app' \
  '  db:' \
  '    ports:' \
  '      - "${CDA_BIND_ADDRESS:-127.0.0.1}:${POSTGRES_PORT:-5432}:5432"' \
  '    logging:' \
  '      driver: json-file' \
  '      options:' \
  '        max-size: "10m"' \
  '        max-file: "3"' \
  > compose.override.yaml
```

## 4. Start PostgreSQL

```sh
podman compose up -d --wait db
podman compose ps
```

The message beginning `Executing external compose provider` is informational.
Podman delegates Compose-file processing to the installed provider while it runs
containers through Podman's socket; it does not start Docker's container engine.

## 5. Start the web host

The configured database password matches the development configuration, so
start the watched web host through the repository script:

```sh
npm run watch
```

Open the local address printed by `npm run watch`.

## Troubleshooting

### Host connections reset or the machine stops

If the app reports `Connection reset by peer`, confirm the selected machine and
its provider:

```sh
podman machine list
podman system connection list
```

The active machine should be `podman-machine-dev` using `applehv`. If it is not,
run the commands from section 2, then recreate the database:

```sh
podman machine start podman-machine-dev
podman system connection default podman-machine-dev
podman compose up -d --wait db
```

The default macOS `libkrun` machine may reset host connections in some setups.
Keep it intact if desired; the Apple Hypervisor machine has separate containers,
networks, and volumes.

### A database container was created before the secret file existed

Remove only the failed container and recreate it. The named database volume is
preserved:

```sh
podman rm -f candoitall-app-db-1
podman compose up -d --wait db
```

### Change the database password

PostgreSQL reads `.secrets/db-password` only when initializing an empty volume.
For the standard `npm run watch` flow, keep the password set to `candoitall`.
To restore that value and recreate disposable development data:

```sh
echo 'candoitall' > .secrets/db-password
podman compose down --volumes
podman compose up -d --wait db
```

`--volumes` permanently removes local PostgreSQL data.

## Remove Podman resources

All commands in this section are destructive. Choose the smallest scope that
matches what you want to remove.

### Remove this repository's database resources

This stops and removes the Compose containers, networks, and named volumes for
this repository. It permanently deletes the local PostgreSQL data:

```sh
podman compose down --volumes --remove-orphans
```

Optionally remove the development images used only by this stack. Do not run the
second command if another local project uses the same PostgreSQL image:

```sh
podman image rm -f candoitall-app:dev
podman image rm -f docker.io/library/postgres:16-alpine
```

### Remove the dedicated macOS development machine

This removes `podman-machine-dev` and every container, image, network, and
volume stored in that machine. It does not delete any other Podman machine:

```sh
podman machine rm -f podman-machine-dev
```

### Remove all unused resources in the active machine

This affects every project in the currently selected Podman machine. Review the
active connection first, then prune unused containers, images, networks, build
containers, and volumes:

```sh
podman system connection list
podman system prune --all --volumes --force
```

To remove another named machine, list the machines and explicitly remove the
one you no longer need. For example, the original `libkrun` machine can be
removed separately:

```sh
podman machine list
podman machine rm -f podman-machine-default
```

## Uninstall Podman and the Homebrew dependencies

First remove every Podman machine you created or no longer need (including
`podman-machine-dev` above). Then uninstall the Homebrew formulae and remove
the extra tap:

```sh
brew uninstall podman krunkit
brew untap slp/krun
```

`brew trust slp/krun` records trusted taps in Homebrew's local trust metadata.
Homebrew currently has no corresponding `brew untrust` command; after `untap`,
the retained trust entry is inert. You can inspect the remaining entries with:

```sh
brew untrust slp/krun
brew trust --json=v1
```

## Optional: build the full containerized stack

The application Dockerfile needs the sibling repositories as named build
contexts. Build with Podman directly, then start Compose without building:

```sh
podman build \
  --build-context components=../CanDoItAll.Components \
  --build-context filetools=../CanDoItAll.FileTools \
  --file src/App/CanDoItAll.Web/Dockerfile \
  --tag candoitall-app:dev \
  .
podman compose up -d --no-build --wait
```
