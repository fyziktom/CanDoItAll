# SB06 project references before implementation

State: `CAPTURED`.

The source listing is `proof/transcripts/sb06-project-references-before.txt`.

- inner AgentFramework Maf/Models/Providers have no Workspace, SharedProviders Http, Web, or UI
  implementation edge;
- outer `Modules.AgentFramework` already references Workspace and SharedProviders Abstractions;
- Workspace references Abstractions, never Http;
- Http references only Abstractions;
- Composition owns the Http implementation reference and concrete wiring.

The force-refreshed SB05 closure snapshot `snap-20260825070408-300644c7` is the SB06 graph baseline:
14 scoped product projects, 34 direct references, zero project cycles, and no error finding.
