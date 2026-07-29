# Integration

Integration projects adapt separately owned libraries and external systems to application
contracts.

| Project | Responsibility |
|---|---|
| [CanDoItAll.FileTools.Integration.Abstractions](CanDoItAll.FileTools.Integration.Abstractions/README.md) | Typed file-access, browse, and scope contracts |
| [CanDoItAll.FileTools.Integration](CanDoItAll.FileTools.Integration/README.md) | Authorized file-tool, storage, download, and desktop-launch adapters |

External DTOs and protocol details stay inside the implementation project. Product
domains consume the abstractions.
