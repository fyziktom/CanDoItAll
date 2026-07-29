# CanDoItAll.RpiValidationArtifacts

## Purpose

Utility project for producing a self-signed Raspberry Pi TLS certificate and private key
plus a private IPFS swarm key.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build tools/Validation/CanDoItAll.RpiValidationArtifacts/CanDoItAll.RpiValidationArtifacts.csproj
$rpiDeployRoot = ".artifacts\rpi-validation"
$rpiIpAddress = "192.0.2.10"
dotnet run --project tools/Validation/CanDoItAll.RpiValidationArtifacts/CanDoItAll.RpiValidationArtifacts.csproj -- $rpiDeployRoot $rpiIpAddress
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.RpiValidationArtifacts.csproj](CanDoItAll.RpiValidationArtifacts.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

The run command requires a deployment-root path and a valid IP address. It creates the
deployment directories when needed and writes:

- `certs/candoitall-rpi.crt.pem`: a self-signed server certificate with the supplied IP
  address as a subject alternative name
- `certs/candoitall-rpi.key.pem`: the corresponding unencrypted PKCS#8 private key
- `ipfs/swarm.key`: a newly generated private IPFS swarm pre-shared key

The private key and swarm key are sensitive deployment secrets. Restrict their
permissions, transfer them only through an approved secure channel, and never commit or
attach them to logs or validation evidence. Each run replaces all three files at the
selected deployment root, so rerunning against an active deployment invalidates the
previous certificate pair and swarm membership.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
