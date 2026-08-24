# Current API, authorization, and OpenAPI

## API composition

`ApiEndpointRouteBuilderExtensions.cs` maps the application's optional `/api` group and
registers endpoint families. API endpoints use Minimal API conventions, tags, explicit
`Produces` declarations, structured error helpers, and authorization policies.

The provider-sharing endpoint family should follow this pattern:

- endpoint definitions stay in `CanDoItAll.Web/Api`;
- business behavior stays in module/application services;
- HTTP transport details stay behind integration abstractions;
- the endpoint mapper is added once to the root API group.

## Authorization

API access uses optional JWT Bearer authorization and exact scopes. Scope names are centralized
in `ApiAccessScopeNames`; policy names and registration live in
`ApiAuthorizationPolicies` and `ApiServiceCollectionExtensions`.

The current umbrella `api` scope convention may remain compatible, but provider sharing needs
granular scopes:

- `api.shared-providers.catalog.read`
- `api.shared-providers.invoke`

A future management scope is not required for v1 because publication/source administration is
a local UI/application service. Do not expose remote provider administration merely for test
setup.

## Error conventions

Native CanDoItAll routes use the existing `ApiErrorResponse`/Problem Details direction.
OpenAI-compatible routes require the expected OpenAI error envelope for client compatibility.
The two surfaces must share typed internal errors but not force one external envelope onto the
other.

## OpenAPI

The Web host exposes `/openapi/v1.json` and `/swagger/v1/swagger.json`. SharedInfo retains a
snapshot, provenance manifest, hash, operation sets, and API-specific skills.

The final implementation must:

1. document every native and OpenAI-compatible operation;
2. keep the two JSON endpoints byte-identical where the current validator requires it;
3. capture from a clean final host;
4. update `_candoitall-api-shared`;
5. add `candoitall-api-shared-providers`;
6. update route-parity appendices and validators.

## Access-context gap

There is no shared request-scoped access-object reference today. Adding it directly to every
request DTO would make later EGCP evolution expensive.

The target adds a bounded `AccessContextReference` value object and scoped accessor outside
provider DTOs, populated from `CanDoItAll-Access-Context-Ref`. W3C trace context continues to
serve distributed tracing; the access reference is separate business correlation.
