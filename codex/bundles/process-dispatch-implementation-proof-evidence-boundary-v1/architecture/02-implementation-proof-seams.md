# Implementation Proof Seams

## Seams to isolate

1. Contract text construction and stack detection.
2. Receipt normalization and ordering.
3. Concrete product path classification.
4. Concrete mutation/read proof.
5. Runnable app proof.
6. DotNet host discovery and invalid host shape.
7. Carried/historical proof state.
8. Process mock proof satisfaction.
9. Completion/recovery consumers.

## Migration order

Do not migrate consumers before helpers have focused tests. Do not migrate carried proof before receipt/path semantics are stable. Do not migrate completion/recovery consumers before exact summary strings and missing-tool semantics are proven.
