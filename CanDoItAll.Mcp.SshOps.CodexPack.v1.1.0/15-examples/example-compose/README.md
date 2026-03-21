# Example compose stacks

These files are reference patterns, not production-ready drop-ins.

## Included examples
- `infra-traefik/`: shared edge router with ACME and dashboard protection
- `app-stack/`: .NET app + PostgreSQL + private IPFS on internal backend network
- `ipfs-stack/`: standalone private Kubo/IPFS stack

## Important notes
- Replace all placeholder domains, secrets, hashes and fingerprints.
- Prepare `acme.json` with mode `600` before first Traefik start.
- Do not publish PostgreSQL or IPFS API ports by default.
- Remove public IPFS bootstrap peers and use only controlled peers.
- Start with Let's Encrypt staging before production.
