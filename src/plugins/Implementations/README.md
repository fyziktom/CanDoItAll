# Bundled Plugin Implementations

| Project | Responsibility |
|---|---|
| [Docker](CanDoItAll.Plugin.Docker/README.md) | Governed Docker workflow integration |
| [Email](CanDoItAll.Plugin.Email/README.md) | Shared email plugin behavior |
| [Gmail](CanDoItAll.Plugin.Gmail/README.md) | Gmail OAuth and mail integration |
| [Office 365](CanDoItAll.Plugin.Office365/README.md) | Microsoft 365 OAuth and mail integration |

Each implementation maps an external protocol to plugin contracts and keeps credentials
behind approved secret and OAuth boundaries.
