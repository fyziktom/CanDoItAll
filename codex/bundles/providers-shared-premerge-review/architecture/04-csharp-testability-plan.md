# C# Testability Plan

Characterize first: current error:null rejection, clean-EOF failure, quoted credential miss and orphan details must fail a focused regression before repair. Use synthetic payloads and real PostgreSQL only where storage semantics matter.

Isolated tests: response policy, URI policy, redaction, timeout classification, constant-set request parity, memory lifetime. Avoid full runtime construction for pure policies.

Integration: public Web endpoint plus actual pinned OpenAI streaming client; imported source materializer/mapper/HTTP selector; actual provider decorator -> recorder -> persisted history; retained retry input cleanup; generated OpenAPI semantic assertions; development-to-final populated migration rehearsal.

Negative cases: non-null error, unsupported fields, malformed/truncated streams, unexpected cancellation, public/non-loopback HTTP, revoked publication/credential, capture-boundary secret, active referenced detail, zero test discovery, stale export host.

Composition: existing registrations resolve actual repaired adapters; no fake-only registry path. New provider kind through existing typed driver seam must not require a runtime partial. Record independent class tests and downstream smoke, not only row counts/non-null assertions.
