# Projects HTTP schema preparation for SPA generation

Status: source authored and independently reviewed on 2026-10-16 in the managed
runtime checkout. It is not built or executed. Main Stable is running against its
earlier frozen checkpoint. No SPA client has been generated from this candidate.

The schema transformer now describes the actual custom-converter representations
of dataset namespace, caller binding, project admission, original definition
expectation, request version and result version. The three commands require their
complete command shape, including explicit replacement arrays and the original
expectation for updates. External-identity names and nullable values are preserved.
The existing canonical string epoch transport and owner codecs are unchanged.

Accepted, rejected and unconfirmed responses expose literal status values.
Write403 explicitly permits both owner rejection and the access-control error
envelope; lookup403 similarly permits its closed lookup union or access-control
error. Write415 describes the handler's rejected envelope. Coherent read metadata
now includes its existing403 access refusal and409 dataset-change error, alongside
404. These are documentation corrections; handler dispatch and authorization
behavior are unchanged.

Three new actual OpenAPI integration Facts cover nested original expectations and
versions, full/scalar command distinction, ordered child serialization, nullable
new IDs/dates, numeric enums, status literals, lookup discriminator/query and
failure response contracts. The last also sends an actual non-JSON write and
checks its415 body. The existing authenticated HTTP case now checks the distinct
error envelope for both middleware write refusal and handler read refusal.

Source-derived focused selection, with no dynamic data:

| Class | Facts | Inline rows | Expected rows |
| --- | ---: | ---: | ---: |
| `ProjectDefinitionHttpTests` | 6 | 8 | 14 |
| `ProjectDefinitionHttpBodyTests` | 8 | 6 | 14 |
| `ProjectDefinitionHttpIntegrationTests` | 5 | 10 | 15 |
| `ProjectOperationEpochHttpIntegrationTests` | 3 | 0 | 3 |
| `ProjectDefinitionOpenApiSchemaTests` | 3 | 0 | 3 |
| Total | 25 | 24 | 49 |

```text
FullyQualifiedName~ProjectDefinitionHttpTests|FullyQualifiedName~ProjectDefinitionHttpBodyTests|FullyQualifiedName~ProjectDefinitionHttpIntegrationTests|FullyQualifiedName~ProjectOperationEpochHttpIntegrationTests|FullyQualifiedName~ProjectDefinitionOpenApiSchemaTests
```

Direct Web/dependency build, exact discovery, execution and portability enforcement
remain pending. Actual emitted schemas must pass these assertions before using
the candidate as generator input. Generated TypeScript serializers still require
round-trip proof, exact epoch/timestamp strings and no implicit write retries.
The SPA generator's empty-schema fallback must reject protected contracts, and
the shared browser store needs its reviewed asset distribution and HTTP adapter.
This preparation does not close either SPA writer or any legacy mutation route.
