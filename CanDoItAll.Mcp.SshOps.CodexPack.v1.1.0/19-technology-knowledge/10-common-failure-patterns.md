# Časté failure patterny

## Síť a SSH
- špatný host fingerprint,
- špatný private key format,
- timeout při navazování spojení,
- chybějící sudo práva.

## Docker a Compose
- invalid compose YAML,
- missing external network,
- image pull timeout,
- unhealthy service.

## Traefik a TLS
- chybné labels,
- špatný resolver,
- obsazený port 80/443,
- rate limited cert issuance,
- špatná práva na `acme.json`.

## PostgreSQL
- healthcheck fail,
- bad credentials,
- corrupt volume permissions.

## IPFS
- public bootstrap peers not removed,
- public API exposure,
- wrong swarm key,
- zero peers in supposed private network.
