# Optional offline OpenAPI review helper

`openapi_review.py` is a dependency-free Python 3.10+ helper supplied with this preparation
pack. It reads local UTF-8 JSON files only. It never fetches references, contacts a server,
changes the document or guesses field meanings. It is not part of the product until an
explicit, tested adoption into the appropriate repository.

## Commands

Run from the unpacked directory:

```powershell
python ./tools/openapi_review.py audit ./actual.openapi.json --output ./audit.json
python ./tools/openapi_review.py compare ./before.openapi.json ./after.openapi.json --output ./contract-diff.json
python -m unittest discover -s ./tests -v
```

Both source documents must be generated from known builds/configurations. The helper does
not generate them. Do not substitute the historical SharedInfo snapshot for the same-source
before-document when determining whether your annotation work changed the contract.

Exit codes: `0` means no helper findings (audit) or no non-prose delta (compare); `1` means
findings/deltas require review; `2` means input/tool execution failed. `--output` writes only
the report to the exact requested path; the directory must already exist. Reports list JSON
Pointers and change categories rather than dumping potentially sensitive example values.
The audit can repeat a description check when a reusable parameter/response is used more
than once; its check totals are not a count of unique public CLR properties.

## What audit checks

- Nonblank operation summary and description, parameter/header/request-body/response
  descriptions, named schema descriptions and individual property descriptions.
- Simple obvious placeholder language as warnings, not a natural-language quality score.
- Local references in the inspected schema/parameter/response/request-body/path graph,
  with cycle guards and JSON Pointer escaping; external references are reported, not fetched.
- Schema properties, array/map items and composition branches, including local recursive
  models. Generic type-level text does not substitute for a property's specific role.
- Duplicate operation identifiers; a missing identifier is a warning, not an invitation to
  rename existing operations automatically.
- Operations under paths, webhooks and inline/referenced callbacks used by operations.
  Reused path-item targets are checked once; the operation count is not a substitute for a
  complete method/route inventory from the running host.

A JSON schema boolean or dynamic reference may need a manually reviewed disposition.
Unreferenced reusable callback components, security-scheme and link descriptions, runtime
endpoint discovery, CLR-to-schema mapping and undocumented excluded endpoints are outside
this helper's complete audit coverage. The implementation agent must include them in its
maintained coverage inventory where applicable.

## What compare checks

It removes prose at known OpenAPI/JSON Schema positions and compares everything else
conservatively. In particular it preserves:

- business members literally named `description`, `summary`, `examples`, and similar;
- methods, routes, operation identifiers and schema identifiers/references;
- types, nullability, required sets, enum tokens, formats, validation constraints, defaults,
  read/write markers, additional-property policies and discriminators;
- security requirements, response statuses and media types, server URLs and unknown extensions.

Descriptions and examples at recognized documentation locations are excluded. Unknown
locations, security-scheme/link annotations and extension payloads are intentionally retained,
so some documentation-only changes will still require manual disposition. Array order is
not normalized, even for fields that another validator considers set-like. This favors
false positives over silently hiding a real difference.

A newly accurate response schema/status can legitimately produce a delta without changing
runtime behavior. Record it as a **metadata correction with runtime evidence**, not as an
unreviewed breaking change or a reason to discard the helper. A clean compare result only
means this structural comparison found no delta: examples may still be wrong, prose may
still mislead, and generated clients may still expose a pre-existing unusable contract.

## Limits and validation

This is not a complete OpenAPI validator, JSON Schema example validator, semantic
compatibility classifier, security audit or proof that every reachable type was documented.
It does not understand ASP.NET serialization behavior, XML comments, business units,
permission rules, mutation guarantees or the truth of a description. Use a version-compatible
OpenAPI parser/schema validator plus product HTTP tests and browser inspection.

Duplicate JSON members, NaN/Infinity and unsupported document versions are rejected.
Inputs above 100 MiB are rejected. The parser accepts OpenAPI 3.0.x and 3.1.x for review;
acceptance by this helper does not certify conformance to either specification.

The reviewer ran **37 synthetic self-tests** covering traversal, recursive/local/external
references, escaped names, metadata-vs-business-property comparison, required/null/default/
enum/security changes, and command-line exit behavior. All passed. No CanDoItAll runtime
OpenAPI export was audited by this helper during package preparation.
