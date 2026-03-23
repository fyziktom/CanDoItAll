# Reference links and notes

Last reviewed: 2026-03-20

## MCP / .NET
- Official MCP C# SDK: https://github.com/modelcontextprotocol/csharp-sdk
- ModelContextProtocol NuGet: https://www.nuget.org/packages/ModelContextProtocol/
- .NET 10 overview: https://dotnet.microsoft.com/en-us/download/dotnet/10.0
- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core

## SSH
- OpenSSH ssh_config(5): https://man.openbsd.org/ssh_config
- SSH.NET repository: https://github.com/sshnet/SSH.NET
- SSH.NET NuGet: https://www.nuget.org/packages/SSH.NET/

## Docker / Compose
- Install Docker Engine on Ubuntu: https://docs.docker.com/engine/install/ubuntu/
- Linux post-installation steps: https://docs.docker.com/engine/install/linux-postinstall/
- Docker Compose startup order: https://docs.docker.com/compose/how-tos/startup-order/

## Traefik / TLS
- ACME / certificate resolvers: https://doc.traefik.io/traefik/master/reference/install-configuration/tls/certificate-resolvers/acme/
- Docker provider: https://doc.traefik.io/traefik/master/providers/docker/
- Let's Encrypt rate limits: https://letsencrypt.org/docs/rate-limits/

## IPFS / Kubo
- Kubo Docker docs: https://docs.ipfs.tech/install/run-ipfs-inside-docker/
- Kubo RPC API docs: https://docs.ipfs.tech/reference/kubo/rpc/
- Kubo bootstrap commands: https://docs.ipfs.eth.link/reference/cli/

## PostgreSQL
- PostgreSQL Docker image docs: https://hub.docker.com/_/postgres
- pg_isready reference: https://www.postgresql.org/docs/current/app-pg-isready.html

## Notes
Při skutečné implementaci ověř:
- aktuální stabilní verze balíčků,
- aktuální image tagy,
- aktuální kompatibilitu s Ubuntu verzí cílového hostu,
- aktuální ACME / DNS provider požadavky.
