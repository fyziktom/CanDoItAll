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
| Defining provider/model thinking-effort support | `CanDoItAll.AgentFramework.Models` |
| Discovering model capability metadata and mapping native values | provider driver |
| Selecting an agent's provider, model, and thinking-effort override | Agent management UI through provider-neutral Models contracts |

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

## Agent thinking-effort configuration

Agent configuration uses a nullable typed override. `null` means provider
default, so adapters omit the native parameter unless the provider profile
explicitly configures a supported default. `None` is a concrete value and is
not interchangeable with inheritance.

Capability resolution is provider/model-specific and returns `Supported`,
`Unsupported`, or `Unknown`, its definition/discovery source, native control
mode (`BooleanToggle` or `EffortLevels`), and the exact allowed effort values.
Direct OpenAI models use one explicit registry whose
rows define the stable model ID, status, allowed values, and transports. Only
an exact ID or a strict `-yyyy-MM-dd` snapshot matches; near matches remain
`Unknown`. This is deliberately a table, not a family-prefix heuristic,
because effort sets differ between adjacent model generations and Pro models
are Responses-only.

Azure deployment names are operator-owned and cannot safely imply the
underlying OpenAI model. Azure therefore requires provider-scoped capability
metadata under `modelThinkingEffortCapabilities` and otherwise remains
`Unknown`; a health result that does not report capabilities must not erase
existing defined metadata. Provider kind and capability metadata are stored in
the canonical workspace provider `ExtraSettingsJson`, projected into the
runtime profile, carried through the provider editor, and written back on
health refresh or ordinary save. This makes administrator-defined Azure
deployment metadata and discovered Ollama metadata durable across database
reloads rather than merely process-local.
Capability metadata is parsed strictly on capability read/write/save paths:
malformed JSON and non-object roots fail before persistence. Ordinary provider
saves compare the prior and edited provider identity, so discovered metadata
is retained only while kind and normalized base URL still identify the same
provider. Defined administrator metadata is not silently converted into
discovery data.

Ollama discovery retains each model's family and advertised `thinking`
capability. `/api/tags` is accepted as a positive signal; otherwise the driver
queries the model's `/api/show` metadata sequentially because Ollama 0.32.5 can
omit thinking from tags. A discovered `Supported` or `Unsupported` result takes
precedence over bounded known-family definitions, while `Unknown` does not hide
a known supported family. Unrecognized custom models remain `Unknown` rather
than being guessed. Models with Boolean thinking control expose only `None` and
`Medium`, which map to native `false` and `true`.
GPT-OSS is explicitly level-controlled and exposes only `Low`, `Medium`, and
`High`; thinking cannot be disabled for that family. This distinction is part
of the neutral capability record, so adapters do not infer native shape from a
provider-wide rule.

Persisted enum compatibility is explicit. Existing numeric values remain
`None=0`, `Low=1`, `Medium=2`, `High=3`, `ExtraHigh=4`, and `Max=5`;
`Minimal=6` was appended and display/native ordering is defined separately.
This avoids silently reinterpreting existing database values while still
supporting original GPT-5 models.

The canonical agent key is nested
`modelParameters.reasoningEffort`. Legacy root/nested `reasoningEffort` remains
provider-neutral. Legacy Ollama `think` values are read only after the selected
provider is known to be Ollama; they are ignored for OpenAI/Azure and removed
on canonical save. The editor records an explicit reset intent so choosing
Provider default removes a legacy `think` value instead of remigrating it;
untouched legacy Ollama agents still migrate canonically. Save/reset
canonicalizes or removes all aliases without changing unrelated JSON. An incompatible agent override
fails before provider dispatch. A provider-wide default is ignored for an
unsupported or unknown model so one profile can still serve mixed model
families. For a supported model, an invalid provider default is an explicit
configuration error: the dialog blocks save until a valid agent override is
selected, and Core enforces the same invariant for non-UI callers.

Provider adapters own native values. Direct OpenAI and Azure dispatch resolve
the same effective typed value and map it to their SDK or wire contract;
`Minimal` and `Max` use the raw OpenAI option path where the MAF SDK enum cannot
express them. Azure direct dispatch honors the profile transport: Chat
Completions uses the deployment endpoint and flat `reasoning_effort`, while
Responses uses `/openai/v1/responses`, `model`/`input`, `store: false`, and
nested `reasoning.effort`, matching the MAF path. Ollama maps Boolean-control `None`/`Medium` to `false`/`true`,
maps GPT-OSS `Low`/`Medium`/`High` to lowercase strings, and maps inheritance to
omission. Installed Ollama 0.32.5 probes confirmed Boolean behavior for Qwen
3.5 and Gemma 4, level-only behavior for GPT-OSS, `/api/show` discovery for
DeepSeek-R1, and no thinking capability for Llama 3.2. Invalid detail responses
fail discovery explicitly. The neutral capability shape allows later
provider/model definitions to expose different sets without changing agent
configuration or Razor code.

MAF receives already-resolved agent `ChatOptions`. Its Ollama defaults wrapper
therefore consumes effective thinking and output-token values from those
options before considering provider-only configuration. This preserves the
same agent-over-provider precedence as Core and the direct driver, including
when a valid agent override masks an invalid provider default.

Only Agent Details Runtime settings can edit the agent override. Chat and
per-run surfaces do not own or expose it. UI components render the neutral
capability result and never parse provider JSON or choose wire values.

The maintained capability sources are the official [OpenAI model
catalog](https://developers.openai.com/api/docs/models/all), individual OpenAI
model pages such as [GPT-5.5](https://developers.openai.com/api/docs/models/gpt-5.5)
and [GPT-5.5 Pro](https://developers.openai.com/api/docs/models/gpt-5.5-pro),
and the [Ollama thinking contract](https://docs.ollama.com/capabilities/thinking).
Undocumented model IDs are not inferred or added to the registry.

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
- Thinking-effort policy tests cover supported, unsupported, and unknown
  models; provider/agent precedence; explicit reset; legacy reads; and invalid
  override rejection.
- Provider and MAF tests prove Ollama discovery, family-specific string/Boolean mapping,
  provider-default omission, OpenAI/Azure transport mappings, preservation of
  provider-scoped Azure capability metadata through the database-backed
  registry, and non-exposure of internal thinking text.
- Component and browser tests prove Runtime-only override, save/reopen,
  provider-default reset, provider/model transitions, and unsupported/unknown
  explanations.
- The release gate is the zero-warning Release solution build, focused unit,
  component, catalog-integration, and deterministic Playwright matrices, exact
  Azure/OpenAI/Ollama wire tests, and live installed-model Ollama validation.
