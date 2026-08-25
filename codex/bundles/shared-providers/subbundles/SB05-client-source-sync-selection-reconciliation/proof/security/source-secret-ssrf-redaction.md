# SB05 secret, SSRF, and logging containment

State: `PASS`.

- Source persistence contains one secret-record ID and no token/authorization/request/response body.
- The typed token renders `[REDACTED]`; catalog requests stringify to the type name only.
- Bearer material is resolved at dispatch and attached only to the outbound catalog request.
- Default named-client logging is removed for catalog and relay clients. Application warnings contain
  only typed failure/status metadata, never URI, token, access context, or remote content.
- Redirects, proxy use, and cookies are disabled. DNS is resolved and classified for every new
  connection, preventing rebinding through cached trust.
- Public policy rejects private, loopback, link-local, multicast, documentation, benchmarking,
  protocol-assignment, 6to4, and other non-global/special-purpose addresses; trusted policy is explicit
  and still denies inherently unsafe destinations.
- Platform TLS certificate validation is not replaced or bypassed.

The final persistence scan, production log-call audit, real named-client URI-log regression, special
address tests, and anti-stub scan pass. The address decision was checked against the current official
IANA registries: `https://www.iana.org/assignments/iana-ipv4-special-registry` and
`https://www.iana.org/assignments/iana-ipv6-special-registry`.
