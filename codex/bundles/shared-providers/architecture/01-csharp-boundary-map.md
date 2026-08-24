# C# boundary map

Required by the C# architecture bundle guard.

## Target boundaries

```mermaid
flowchart LR
    ClientUI[Workspace provider UI]
    Workspace[Workspace shared-provider application services]
    EF[(PostgreSQL)]
    AFAdapter[Workspace to AgentFramework projection]
    MAF[Existing MAF OpenAI-compatible runtime]
    Web[CanDoItAll.Web endpoints]
    Abs[SharedProviders.Abstractions]
    Http[SharedProviders.Http]
    CentralProfile[Central Workspace provider profile]
    Upstream[OpenAI / Ollama / ComfyUI]
    EGCP[Future EGCP]

    ClientUI --> Workspace
    Workspace --> EF
    Workspace --> Abs
    AFAdapter --> Workspace
    AFAdapter --> MAF
    Web --> Workspace
    Web --> Abs
    Workspace -->|ISharedProvider transport ports| Abs
    Http --> Abs
    Web -. composition .-> Http
    Workspace --> CentralProfile
    Http --> Upstream
    ClientUI -->|local shared profile| MAF
    MAF -->|OpenAI-compatible HTTP| EGCP
    EGCP -. optional future hop .-> Web
```

The diagram shows logical ownership. Composition registration belongs in the outer
Composition/Web layer; Workspace never references the concrete HTTP implementation.

## New lower-level projects

Preferred names, subject to SB00 current-layout verification:

### `src/Integration/CanDoItAll.SharedProviders.Abstractions`

Owns SDK-free, EF-free, ASP.NET-free types:

- public catalog records and protocol constants;
- shared-provider operation/capability enums;
- routing model ID value/codec contract;
- source catalog client port;
- central inference transport port;
- upstream adapter descriptor/support records;
- neutral relay request/response/session contracts;
- typed failure categories;
- no provider SDK types;
- no Workspace entity types;
- no `HttpContext`;
- no secret persistence implementation.

It may reference `CanDoItAll.SharedKernel` only when a current canonical Result/Error/value
object is required. Prefer zero project references when practical.

### `src/Integration/CanDoItAll.SharedProviders.Http`

Owns:

- source `HttpClient` implementation;
- bounded OpenAI-compatible request validation and mapping;
- upstream OpenAI/Ollama/ComfyUI relay adapters;
- adapter registry and target normalization;
- redirects/network enforcement;
- streaming response transport and usage extraction;
- external wire quirks and private DTOs;
- no EF entities;
- no Razor/UI;
- no Web endpoint definitions;
- no direct secret-store lookup.

It references Abstractions and the smallest current lower-level HTTP/shared-kernel projects.
It does not reference Workspace or Web.

If the current dependency graph makes the two-project shape unnecessary, SB00 may collapse them
to one SDK-free integration project only when the before/after dependency proof shows no
contract/implementation inversion. It may not move protocol records into Web or Workspace just
to avoid a project.

## Existing ownership retained

### Workspace

Owns:

- `ProviderSharePublication`;
- `SharedProviderSource`;
- `SharedProviderImport`;
- `SharedProviderInvocationRecord`;
- EF configurations and application services;
- publication eligibility and sanitized projection;
- source/import state machine and reconciliation;
- secret-reference selection through existing Security services;
- runtime effective-profile materialization;
- local UI view models.

New types live in cohesive files/folders, not `WorkspaceModels.cs`.

### Web

Owns:

- route mapping;
- body/request limits attached to routes;
- auth policy application;
- native versus OpenAI error envelopes;
- response headers and SSE HTTP semantics;
- access-context middleware binding;
- OpenAPI metadata.

Web delegates routing, policy, persistence, and upstream work.

### AgentFramework module

Owns only the outer projection from Workspace rows into existing AgentFramework profiles. It
may understand `provider.candoitall-shared` origin metadata. Inner MAF/Providers projects do not
understand Workspace source/import entities.

## Forbidden boundary crossings

- MAF/Core -> Workspace/Web/EF/SharedProviders.Http
- Abstractions -> Workspace/Web/UI/EF/provider SDK
- Http -> Web/UI/EF/Workspace entities
- Workspace -> Http concrete classes
- Protocol DTO -> `ProviderProfile`, `Secret`, `DbContext`, `HttpContext`
- Razor component -> direct `HttpClient` or EF context
- API endpoint -> direct upstream provider SDK
