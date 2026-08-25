# SB01 production-source assertions

| Assertion | Production source | Observable consequence |
| --- | --- | --- |
| Strict catalog wire contract is centralized. | `src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderCatalogContracts.cs` | One read-only serializer rejects unknown/duplicate/case-mismatched values, validates shape/capability coherence, and returns recursively copied canonical collections. |
| Strong revisions are derived from sanitized state. | `src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderCanonicalRevision.cs` | Provider/model/capability order does not affect the hash; public health state does; revision self-fields and volatile timestamps cannot. |
| Routing identity is opaque and publication scoped. | `src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderRoutingModelIdCodec.cs` | Only the exact 80-character `sp1` form parses; server resolution requires the publication plus exact model fingerprint. |
| Protocol/base routes are repository owned. | `src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderProtocol.cs` | Native/OpenAI paths and headers are constants; resolving against a source root preserves a reverse-proxy base path. |
| SDK-neutral ports fail predictably. | `src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderPorts.cs` | Invalid bounds/defaults/capability combinations are rejected; catalog success requires ETag/revision agreement; result variants are exhaustive. |
| Access value is exact and bounded. | `src/Integration/CanDoItAll.SharedProviders.Abstractions/AccessContextReference.cs` | Only 1..256 ASCII allowlisted characters are accepted; invalid/default values cannot be read as empty. |
| Web binds once per request scope. | `src/App/CanDoItAll.Web/Api/AccessContextReferenceMiddleware.cs`; `AccessContextReferenceState.cs` | Missing header leaves null; anything other than one valid exact value returns native 400; re-execution cannot overwrite a different value. |
| Scoped state is registered at the API boundary. | `src/App/CanDoItAll.Web/Api/ApiServiceCollectionExtensions.cs` | Consumers receive the narrow read-only accessor while Web retains the internal mutable state. |
| Pipeline position is explicit. | `src/App/CanDoItAll.Web/Program.cs` | Binding occurs after the existing auth pair and before application endpoint dispatch, without replacing auth or tracing. |

The source assertions are exercised by exact passing tests, forbidden-boundary scans, and the
independent frozen-code review. None relies on a test-only production branch or fixture-emitted
signal.
