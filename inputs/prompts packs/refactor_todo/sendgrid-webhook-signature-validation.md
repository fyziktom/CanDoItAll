# SendGrid Webhook Signature Validation Refactor

## Why this refactor is needed
`Controllers/SendGridWebhookController.cs` performs signature verification through static state:
- static `PublicKeyBase64` and static `publicKey`
- hard-coded curve parsing (`"Ed25519"`)
- direct `new RequestValidator()` construction inside the action

This shape makes deterministic integration tests brittle:
- key parsing/verification behavior cannot be swapped in tests
- strict invalid-signature assertions depend on static runtime initialization behavior
- failure mode can surface as `500` instead of explicit `401`

## Minimal seam proposal
1. Add `ISendGridWebhookSignatureVerifier` (single responsibility: verify headers + payload).
2. Implement `SendGridWebhookSignatureVerifier` that:
- loads/validates configured public key during service construction
- encapsulates `RequestValidator` usage
- returns a clear `bool`/result for invalid signatures (no thrown runtime parsing errors in request path)
3. Inject verifier into `SendGridWebhookController` and replace direct static validation with verifier call.

## Suggested file changes
- Add `PVEInvoicing/Emails/SendGrid/ISendGridWebhookSignatureVerifier.cs`
- Add `PVEInvoicing/Emails/SendGrid/SendGridWebhookSignatureVerifier.cs`
- Update `PVEInvoicing/Controllers/SendGridWebhookController.cs` to use DI
- Register verifier in `Extensions/ServiceCollectionExtensionsApp.cs`

## Tests unlocked by this seam
- strict integration test: invalid signature -> `401 Unauthorized`
- deterministic positive-path webhook test with generated signature fixture
- unit tests for edge cases (missing headers, malformed signature, unsupported key format)
