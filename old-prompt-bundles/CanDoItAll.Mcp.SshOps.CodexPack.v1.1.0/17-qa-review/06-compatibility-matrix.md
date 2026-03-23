# Compatibility matrix

## Primary operating system targets
- Ubuntu 24.04 LTS: primary target
- Ubuntu 22.04 LTS: supported target

## Field-validated exception profile
- Raspberry Pi 3 class host on Raspbian 10 / armhf: validation-only exception profile
- Expected constraints: no Docker path, older glibc/OpenSSL baseline, lower CPU and memory budget
- Required mitigations: native systemd services, framework-dependent publish, self-signed TLS for local validation, in-memory app mode, private IPFS without public bootstrap peers
- Treat this as an explicit fallback lane, not as proof that all non-Ubuntu distributions are generally supported

## Runtime and libraries
- .NET 10: required on the control side
- Remote .NET runtime must be checked against host glibc/OpenSSL/ICU before deployment
- Official MCP C# SDK: current stable line at implementation time
- SSH.NET: current stable line at implementation time

## Remote platform
- Docker Engine with Compose plugin: preferred for the standard-host lane
- Native systemd services: required for the legacy-arm-host lane
- Traefik v3.x: preferred
- PostgreSQL 16/17 containers: supported on the standard-host lane
- Kubo/IPFS private swarm: supported in both lanes

## Out of scope for MVP
- General support for arbitrary non-Ubuntu Linux distributions
- Podman
- Swarm / Kubernetes orchestration
- Windows SSH targets
