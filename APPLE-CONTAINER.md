# Apple Container on macOS

Run PostgreSQL in [Apple Container](https://github.com/apple/container) and
the CanDoItAll web host directly on macOS. This avoids building the
containerized `app` service and does not require the sibling
`CanDoItAll.Components` or `CanDoItAll.FileTools` repositories.

Apple Container does not use Compose files for this workflow. Run the commands
below from the repository root.

## 1. Check the Apple Container network subnet

Before creating containers, check macOS's `bridge100` interface. Apple
Container must use the same IPv4 subnet for published ports to relay traffic
correctly:

```sh
ifconfig bridge100 | awk '/inet / { print $2; exit }'
```

If `bridge100` is not `192.168.64.1`, configure Apple Container to use the
address printed by the first command. Create or update
`~/.config/container/config.toml` so that it contains the following section,
replacing the example address with the `bridge100` address on your Mac:

```toml
[network]
subnet = "10.211.56.1/24"
```

Do not add a second `[network]` section if the file already has one; add or
replace only its `subnet` value. The setting is read when the service starts,
so stop and start it after saving the file:

```sh
container system stop
container system start
```

## 2. Start Apple Container

Install Apple's `container` CLI, then start its local service. The first start
may download the runtime components it needs.

```sh
container system start
```

Confirm that the service is available:

```sh
container list --all
```

## 3. Set up the local database password

Create the development-only password file. `.secrets/` is ignored by Git. The
`candoitall` value matches the repository's direct-host development
configuration, so no connection-string environment override is needed:

```sh
mkdir -p .secrets
echo 'candoitall' > .secrets/db-password
chmod 600 .secrets/db-password
```

## 4. Start PostgreSQL

This creates the `candoitall-postgres-data` named volume on first use and
publishes PostgreSQL only on the Mac's loopback interface:

```sh
container run -d \
  --name candoitall-postgres \
  --cpus 1 \
  --memory 1g \
  --publish 127.0.0.1:5432:5432 \
  --env POSTGRES_DB=candoitall_development \
  --env POSTGRES_USER=candoitall \
  --env POSTGRES_PASSWORD_FILE=/run/secrets/db-password \
  --volume candoitall-postgres-data:/var/lib/postgresql \
  --volume "$PWD/.secrets/db-password:/run/secrets/db-password:ro" \
  docker.io/library/postgres:16-alpine
```

The data volume deliberately mounts at `/var/lib/postgresql`, rather than
`/var/lib/postgresql/data`. Apple Container volumes are ext4 filesystems whose
root contains `lost+found`; PostgreSQL refuses to initialize in a non-empty
data directory.

Verify that PostgreSQL is ready:

```sh
container list --all
container logs candoitall-postgres
container exec candoitall-postgres \
  pg_isready -U candoitall -d candoitall_development
```

## 5. Start the web host

The configured database password matches the development configuration, so
start the watched web host through the repository script:

```sh
npm run watch
```

Open the local address printed by `npm run watch`.

## Subsequent sessions

Start the Apple Container service, then restart the stopped database
container:

```sh
container system start
container start candoitall-postgres
```

Stop it when it is no longer needed. The database data remains in the named
volume:

```sh
container stop candoitall-postgres
```

## Troubleshooting

### The web host times out while connecting to PostgreSQL

First confirm a real PostgreSQL connection from macOS (a successful `nc -z`
check alone is insufficient):

```sh
PGPASSWORD=candoitall psql \
  'host=127.0.0.1 port=5432 dbname=candoitall_development user=candoitall sslmode=disable' \
  -tAc 'select 1;'
```

If it times out while `container exec candoitall-postgres pg_isready` succeeds,
the Apple Container network subnet does not match `bridge100`. Follow section
1, then delete and recreate the database container (not its volume) so it
receives an address on the corrected network:

```sh
container delete --force candoitall-postgres
container system stop
container system start
```

Rerun section 4. The named volume is retained, so the existing data remains.

### The database container is stopped

Inspect its logs, then restart it if it stopped after a normal shutdown:

```sh
container logs candoitall-postgres
container start candoitall-postgres
```

If the initialization failed before PostgreSQL created its data directory,
remove the container and its empty volume, then rerun section 4:

```sh
container delete candoitall-postgres
container volume delete candoitall-postgres-data
```

### Change the database password

PostgreSQL reads `.secrets/db-password` only when initializing an empty data
directory. For the standard `npm run watch` flow, keep the password set to
`candoitall`. To restore that value and recreate disposable development data,
remove the container and volume, then run section 4 again:

```sh
echo 'candoitall' > .secrets/db-password
container delete candoitall-postgres
container volume delete candoitall-postgres-data
```

Deleting the volume permanently removes local PostgreSQL data.

## Remove Apple Container resources

All commands in this section are destructive. Choose the smallest scope that
matches what you want to remove.

### Remove this repository's database resources

This permanently deletes the local PostgreSQL data:

```sh
container delete --force candoitall-postgres
container volume delete candoitall-postgres-data
```

Optionally remove the PostgreSQL image if no other local project uses it:

```sh
container image delete docker.io/library/postgres:16-alpine
```

### Remove unused Apple Container resources

These commands affect every Apple Container project on this Mac:

```sh
container prune
container volume prune
container image prune --all
```
