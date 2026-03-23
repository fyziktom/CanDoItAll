# Common failure patterns

## Network and SSH
- wrong host fingerprint,
- wrong private key format,
- timeout while connecting,
- missing sudo rights,
- operator description does not match the actual host distribution or architecture.

## Runtime compatibility
- remote glibc is older than the .NET runtime requires,
- OpenSSL libraries required by .NET are missing,
- ICU is missing and globalization mode is not configured,
- framework-dependent and self-contained publish modes are chosen without checking the target baseline first.

## Docker and Compose
- invalid compose YAML,
- missing external network,
- image pull timeout,
- unhealthy service,
- Docker is installed but not viable on a low-power validation host.

## Traefik and TLS
- incorrect labels or file-provider routes,
- wrong resolver,
- ports 80 or 443 are already occupied,
- rate-limited certificate issuance,
- incorrect permissions on `acme.json`,
- self-signed certificate is generated but not wired into the actual entrypoint.

## Native service deployment
- systemd unit starts as root but the app data directory remains root-owned,
- native service environment variables do not include runtime compatibility paths,
- application ports and reverse-proxy ports do not match.

## PostgreSQL
- health check failure,
- bad credentials,
- broken volume permissions.

## IPFS
- public bootstrap peers were not removed,
- public API exposure,
- wrong `swarm.key`,
- peer count is misinterpreted on a single-node private validation host.
