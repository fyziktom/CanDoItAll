# SB04 supported and denied relay fields

State: `PASS`. This is the implemented compatibility subset, not a generic OpenAI proxy.
Unknown or duplicate JSON members, malformed JSON/UTF-8, excessive depth, and over-limit values
fail before dispatch. Capability-dependent fields must be present in the resolved publication and
adapter intersection; a syntactically valid field is not permission to forward it.

## Chat Completions exact shapes

The root allowlist is exactly `model`, `messages`, `stream`, `tools`, `tool_choice`,
`parallel_tool_calls`, `response_format`, `temperature`, `top_p`, `stop`, `max_tokens`, and
`max_completion_tokens`.

| Field | Implemented policy |
| --- | --- |
| `model` | required publication-namespaced public routing ID; replaced with the current stored upstream model after Workspace resolution |
| `messages` | required array of 1..256 exact role-specific objects; bounded string content or exact Chat content parts |
| `stream` | optional boolean; `true` requires advertised SSE support |
| `tools` | optional array of 1..128 exact nested definitions: `{"type":"function","function":{"name":...,"description"?:...,"parameters"?:{...},"strict"?:boolean}}` |
| `tool_choice` | optional `none`, `auto`, `required`, or exact `{"type":"function","function":{"name":"<declared-name>"}}`; a named choice must match a function declared in the same request |
| `parallel_tool_calls` | optional boolean; the field's presence requires advertised parallel-function support even when its value is `false` |
| `response_format` | exact `{"type":"text"}`, `{"type":"json_object"}`, or `{"type":"json_schema","json_schema":{"name":...,"description"?:...,"schema":{...},"strict"?:boolean}}`; structured forms require advertised support |
| generation controls | finite `temperature` in 0..2, finite `top_p` in 0..1, bounded `stop`, and at most one positive bounded `max_tokens`/`max_completion_tokens` |

Chat message shapes are role-specific. `system`, `developer`, and `user` allow only `role`,
`content`, and optional `name`; `assistant` additionally allows `tool_calls`; `tool` instead allows
`tool_call_id`. An assistant may omit content or use `null` only when it has a valid, bounded
function `tool_calls` array. A Chat text part is exactly `{"type":"text","text":...}`. A Chat
image part is user-only and exactly
`{"type":"image_url","image_url":{"url":"data:image/<png|jpeg|webp>;base64,...","detail"?:"auto|low|high"}}`.
The base64 payload must be non-empty, whitespace-free, valid, and bounded. No current Production
descriptor advertises vision, so that image-input shape remains fail-closed in production.

## Responses exact shapes

The root allowlist is exactly `model`, `input`, `instructions`, `stream`, `tools`, `tool_choice`,
`text`, `temperature`, `top_p`, `max_output_tokens`, and `store`.

| Field | Implemented policy |
| --- | --- |
| `model` | required publication-namespaced public routing ID; replaced with the current stored upstream model after Workspace resolution |
| `input` | required bounded string or array of 1..256 exact message/function-output items |
| `instructions` | optional bounded non-empty string |
| `stream` | optional boolean; `true` requires advertised SSE support |
| `tools` | optional array of 1..128 exact flattened definitions: `{"type":"function","name":...,"description"?:...,"parameters"?:{...},"strict"?:boolean}` |
| `tool_choice` | optional `none`, `auto`, `required`, or exact `{"type":"function","name":"<declared-name>"}`; a named choice must match a function declared in the same request |
| `text` | optional exact wrapper `{"format":{"type":"text"}}`, `{"format":{"type":"json_object"}}`, or flattened schema format `{"format":{"type":"json_schema","name":...,"description"?:...,"schema":{...},"strict"?:boolean}}`; structured forms require advertised support |
| generation controls | finite `temperature` in 0..2, finite `top_p` in 0..1, and positive bounded `max_output_tokens` |
| `store` | omission is canonicalized to `false`; an explicit value must be the JSON boolean `false`; `true`, `null`, and non-Boolean values are rejected before dispatch |

A Responses message item allows only `type`, `role`, and `content`, with optional `type` equal to
`message`; roles are `system`, `developer`, `user`, or `assistant`. Text parts are exactly
`{"type":"input_text","text":...}`. Image parts are exactly
`{"type":"input_image","image_url":"data:image/<png|jpeg|webp>;base64,..."}`. A function
result is exactly `{"type":"function_call_output","call_id":...,"output":...}` and requires
function-tool support. Chat's nested tool/schema/image forms and Responses' flattened forms are
not interchangeable; every cross-surface shape fails closed.

Tool descriptions are strings of at most 4,096 characters. Optional `parameters` and required
structured-output `schema` values must be JSON objects and are bounded to 256 KiB of JSON text;
optional `strict` must be a boolean. Unknown siblings at every nested level are rejected.

## Images Generations exact shapes

The root allowlist is exactly `model`, `prompt`, `n`, `size`, `quality`, `response_format`, and
`output_format`.

| Field | Implemented policy |
| --- | --- |
| `model` | required publication-namespaced public routing ID; replaced with the current stored upstream model |
| `prompt` | required bounded non-empty string |
| `n` | absent defaults to 1; a present value must be an integer in 1..the resolved adapter maximum |
| `size` | optional exact member of `256x256`, `512x512`, `1024x1024`, `1024x1536`, `1536x1024`, or `auto` |
| `quality` | optional exact member of `standard`, `hd`, `low`, `medium`, `high`, or `auto` |
| `response_format` | absent or exactly `b64_json`; URL output is rejected |
| `output_format` | optional exact member of `png`, `jpeg`, or `webp` |

The `n` rule is deliberately explicit: `null`, string, boolean, object, array, fractional number,
integer overflow, zero, negative, or an integer above the adapter limit is malformed and rejected.
The accepted normalized request never silently substitutes 1 for a malformed present value.

## Denied or absent from the relay contract

- caller endpoint, base URL, path, host, headers, organization, project, credential, secret, or
  internal provider-profile object;
- persistence-enabling or malformed `store` values, `background`, provider conversation identifiers, unsupported `include`, caller `user`,
  unsupported modalities, file IDs/uploads, and remote file/image URLs;
- hosted `web_search`, `file_search`, `mcp`, `computer`, `code_interpreter`, shell, retrieval, or
  any non-function tool discriminator;
- cross-surface tool, tool-choice, structured-output, text-part, or image-part shapes;
- image URL responses, private artifact URLs, file paths, raw upstream image locations, and audio.

The five Production descriptors are OpenAI chat, OpenAI image, Ollama local chat, Ollama remote
chat, and ComfyUI image. None advertises vision input. Missing usage remains unavailable; no
request/response body, prompt, tool arguments, image bytes, credential, access-context value,
private URI, or raw upstream failure body may enter invocation audit or usage records.

## August 25 wire-contract reopen proof

The retention rule above was reopened from SB07 and verified at both owned boundaries. The policy
Fact proves omission-to-`false` canonicalization, explicit-`false` preservation, and rejection of
`true`, `null`, and non-Boolean values. The existing compatibility Fact sends the request through
the real Web surface and asserts that the recorded upstream body contains JSON `store: false` for
both omission and explicit-false input. Rejected inputs produce no dispatch.

No Fact was added or removed: relay-policy discovery remained 24 and passed 24/24; compatibility
discovery remained 22 and passed 22/22; streaming discovery remained 12 and passed 12/12. The
entry validator, honest initial/final Unit and Integration builds, clean Web build, and exact
list/run artifacts use the `sb04-reopen-*` transcript prefix.
