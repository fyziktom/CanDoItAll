# Original Request

Source: Codex thread user request.

## Raw Notes

| Note | Exact request text |
| --- | --- |
| `N001` | "We need better storage of the secrets. It is not correct now." |
| `N002` | "On windows we can use DPAPI (use microsoft learn mcp). On mac and linux it must be solved different way. We focus first on windows users so it should not be trouble now." |
| `N003` | "But you must add proper interface for secret vault something like public interface ISecretVault ... SecretVaultOptions ..." |
| `N004` | "and then implementations like DpapiSecretVault, MauiSecureStorageVault, MacOsKeychainSecretVault, LinuxSecretServiceVault, DataProtectionFileVault, AzureKeyVaultSecretVault, HashiCorpSecretVault, InMemorySecretVault" |
| `N005` | "we do not have maui now, so add those just as not implemented yet. But maui actually has already drivers across different os, so later we might wrap our app into maui." |
| `N006` | "we will need to use secrets during some specific actions like inside of workflows, processes ... or in project structure runtime nodes ..." |
| `N007` | "For all those cases you must also prepare proper tools for agents and workflows." |
| `N008` | "For example in settings of agents it must be possible to select if agent can request some of the stored secrets." |
| `N009` | "Same in the workflows. For example when I create step for getting something from http it must offer list of secrets in settings of that step where I can select proper api key that should be used." |
| `N010` | "In all those cases the secrets must stay safe and it must be called just when it is necessary and dropped asap to do not keep it in memory unnecessary long." |
| `N011` | "in selection panel of secrets there must be copy button to copy the secret and secret name edit box must be with option 'show for 30s' and then automatic hide. We should have this as BaseLib component." |
| `N012` | "In project structure we have option to add secret. In that case it must open dialog where I can search proper secret ... Same dialog must also allow easy way to add totally new secret." |
| `N013` | "when you finish update also documentation so it is up to date about this." |

## Working Assumption

Implement the DPAPI-backed Windows path completely. Add the other named providers as explicit unsupported implementations behind the same interface, because silently choosing a weaker storage provider would violate the request and the repo's security posture.
