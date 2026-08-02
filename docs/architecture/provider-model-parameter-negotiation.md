# Provider Model-Parameter Negotiation

## Context

An OpenAI Chat Completions execution using `gpt-5.6-terra` failed before the
model could run because the request combined function tools with a non-`none`
`reasoning_effort`. The provider rejected that combination with HTTP 400.
Neither the agent nor its provider profile configured reasoning effort. The
provider's implicit/default reasoning still formed the rejected combination,
so the failure was not caused by the HR agent definition or by the Gardener
agent's tool grants.

The existing model-parameter policy considered provider kind, transport, and
model, while the runtime attached function tools only after it built model
options. Each part was individually supported, but their combination was not.
Retrying the rejected request would be deterministic and switching transports
implicitly would override the configured provider profile.

## Responsibility inventory

| Responsibility | Owner |
|---|---|
| OpenAI model/transport/invocation compatibility | `CanDoItAll.AgentFramework.Providers` |
| Provider-neutral model identifiers and parameter values | `CanDoItAll.AgentFramework.Models` |
| Mapping effective parameters to Microsoft.Extensions.AI/OpenAI SDK types | `CanDoItAll.AgentFramework.Maf` |
| Discovering the effective tool set and composing agent/run options | MAF runtime composition |
| Raw provider HTTP request/response adaptation | provider driver |
| Safe, actionable provider-failure presentation | AgentFramework Core |
| Selecting an agent's provider, model, and capabilities | HR/agent management, not compatibility negotiation |

The dependency direction remains unchanged: MAF consumes the provider policy,
which consumes Models. No provider policy depends on SDK types, no domain
contract depends on SDK types, and no new project boundary is required.

## Decision

1. The OpenAI compatibility policy accepts a typed invocation-feature set.
   Function tools are the first feature because they participate in the failing
   request constraint.
2. Resolution returns both requested and effective reasoning effort plus a
   typed adjustment reason. This keeps the compatibility decision testable and
   makes adjustment observable without parsing log text.
3. The evidenced `gpt-5.6-terra` model using OpenAI Chat Completions with
   function tools sends an explicit reasoning effort of `none`. This also
   covers profiles whose effort is omitted, because the provider default can
   still form the rejected combination. Other GPT-5.6 models and Azure remain
   unchanged until provider evidence establishes the same constraint.
4. Responses executions and Chat Completions executions without function tools
   retain their configured reasoning effort.
5. A MAF `IChatClient` adapter inspects the final merged request immediately
   before OpenAI Chat Completions inference. This single boundary therefore
   covers base options, per-run overrides, approvals, polling, handoffs,
   finalizer repair, and hosted-agent callers. It clones adjusted options,
   retains every function tool, handles transport-native `max` options, and
   records a correlated warning. The empty-completion retry decorator remains
   outside this adapter so each provider attempt is normalized.
6. The selected provider and transport are not changed implicitly. Operators
   who want reasoning with GPT-5.6 tool calls should select a Responses profile.
7. The runtime does not retry this HTTP 400. A deterministic parameter failure
   is repaired before dispatch; replay after a tool-capable request would add
   complexity and could become unsafe as provider behavior evolves.
8. Exact provider errors for this incompatibility receive a bounded,
   secret-safe actionable explanation as defense in depth. Unknown HTTP 400
   responses keep the existing generic provider-error treatment.
9. Direct provider-driver chat requests currently carry no function tools and
   retain their existing reasoning behavior. If that contract later gains tool
   definitions, it must supply the same typed invocation features rather than
   duplicate compatibility rules.

## Pattern and alternatives

This uses a pure policy plus typed value object and an adapter at the external
inference boundary. A strategy interface was rejected because there is one
closed compatibility decision and no replaceable policy implementation.
Threading a tool flag through every runtime call path was rejected because
late per-run option merging and hosted-agent callers could bypass it. Automatic
migration to Responses was rejected because transport is operator-selected
configuration. Provider-error retry was rejected because the request is
invalid rather than transient.

## Validation contract

- Isolated policy tests cover GPT-5.6 Chat Completions with and without
  function tools, explicit and inherited effort, and Responses preservation.
- MAF adapter tests prove the effective OpenAI SDK option is `none` for the
  incompatible combination.
- Adapter and composition tests prove final merged function tools are retained
  while reasoning becomes `none`, including transport-native `max` options.
- Failure-formatting tests prove exact classification and redaction behavior.
- The focused test suite, solution build, and a live tool-enabled GPT-5.6 Terra
  run on the rebuilt port-5032 instance form the release gate.
