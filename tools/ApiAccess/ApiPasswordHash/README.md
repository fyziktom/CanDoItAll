# API administrator password hash helper

Generate an operator-owned password hash for `Api:BootstrapAdmin:PasswordHash`.
The executable shares the server's password rules and ASP.NET Core Identity hasher.
It does not read or modify deployment configuration.

From the repository root:

```powershell
dotnet build tools/ApiAccess/ApiPasswordHash/ApiPasswordHash.csproj --configuration Release
dotnet tools/ApiAccess/ApiPasswordHash/bin/Release/net10.0/ApiPasswordHash.dll
```

Interactive input is hidden. Trusted provisioning may supply one bounded line through
standard input. Password arguments are rejected; never put passwords in command lines,
shell history, fixtures or logs. Passwords are 12–256 characters and are not trimmed.
Successful standard output contains only the encoded salted hash; diagnostics go to
standard error. Invalid input exits unsuccessfully without echoing the password.

Install the hash through private deployment configuration and protect it along with the
separate JWT signing key. See [API users and deployment access](../../../docs/api-user-access.md)
for exposure switches, TLS/proxy configuration, rotation and compatibility.

Validation: `ApiCredentialRulesTests` exercises the shared hasher and bounds.
`ApiAccessDeploymentTests` invokes this executable through standard input, authenticates
against the production host, rotates the administrator password and checks that unrelated
machine credentials remain usable. These tests generate their own credentials.
