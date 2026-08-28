# Project image recovery semantic verifier

Status: Requested behavior, architecture and final broad regression review pass with
documented unchanged baseline failures. Reviewer: primary implementation agent,
not an independent reviewer. No clean-repository claim.

## Raw-note adequacy

N015/R15 is the exact project/run incident, not the earlier screenshot's model label.
Original run bfb2e58e-411f-4766-be91-ea952333bba1 used gpt-5.6-luna and failed with
HTTP 401 before tools. Client1 expired four hours earlier. The UI needs actionable
safe status, not the remote exception text. The source still rejects expired tokens.

Credential renewal alone was insufficient. The renewed producer path exposed two
more faults: unsupported 1536x864 options with opaque tool feedback, then valid
generated-image data rejected by a text-only limit. Both were reproduced and fixed
in their existing owners, without a new abstraction, silent coercion or bypass.

## Source and boundary review

- Core formatter consumes existing typed status and source-token identity. Static
  401/403 guidance does not claim definite expiry or disclose remote diagnostics.
- MAF retains SDK and HttpRequestException status while preserving the sanitized
  boundary. Existing remote-provider bodies and credentials never become display text.
- Source HTTP policy maps upstream credential failure to 502, distinguishing it from
  source authorization. Local credentials are not mislabeled as shared source tokens.
- Existing image-tool contract exposes supported values through schema descriptions.
  Existing IAgentToolFailure/MAF mapping carries retryable safe option errors. Arbitrary
  invalid input is not reflected, and no option is silently rewritten.
- Http policy validates image base64 independently of text size. Normalize enforces
  the existing aggregate request budget before JSON/string allocation. It still
  rejects oversized text/request bodies, unsupported roles/capabilities/MIME, remote URLs,
  malformed base64 and unknown sibling fields. Base64 validation no longer allocates
  a decoded buffer just to discard it. Both chat protocols have boundary tests.
  Chat image content remains user-only; Responses retains its existing allowed
  message roles. This repair does not redefine those separate protocol policies.
- No persisted-schema/public property-type/project-reference change, new interface, service
  locator, test-only branch, partial-class expansion, alternate provider or mocked
  upstream in live acceptance. Existing tool and relay responsibilities stay separate.
- No component or layout edit. Existing UI dialogs and governed file authority are
  exercised at 1920x1080. Raw unsigned file routes remain denied; the real UI preview works.

## Producer-to-consumer evidence

- Failing-first status, option and image-size tests precede each repair. Final focused
  sets contain 69 and 60 distinct passing cases; exact discovered identities match TRX.
- Final Unit: 7059 pass/1 fail, total 7060. The unchanged llama3.2 price-row fixture
  failure already occurs in SB06/SB07/SB09. An earlier process-timing failure passed
  all 13 isolated cases and the final complete Unit run; it is not hidden as green.
- Final Integration: 1133 pass/10 fail/1 opt-in skip, total 1144. Exact frozen
  discovery reconciles after one deferred theory expands into six cases. All ten
  failed identities and complete messages match SB09, excluding generated GUIDs and
  one ephemeral localhost port. No new failed identities or causes in either suite.
- Actual UI-issued token has catalog/invoke scopes only and expires 2026-08-29 10:50 UTC.
  The original failed run and all user configuration/history are preserved.
- Actual cf11744f-0b2a-4426-a1ac-9a77983da4aa generated one real 1536x1024 PNG via
  gpt-image-1-mini, attached it under Main and read back its metadata/content.
- Asset custom:e06c5abebeb9430c9f623b7b56e4d39b is visible in Object index and the
  governed preview. Its pixels show a calculator, display 2,484 and a History column.
- Final-image UI run 7adcda1b-ceb9-4dcc-a9bc-85dc2587ee4a invoked the exact image
  analysis tool against that asset, returned matching visual details, and changed
  no content. Source metadata records successful complete 1952/279-token vision
  usage and successful complete image count 1. This is not model self-report alone.
- Image -3 is live on 5210/5212/5214, each returns HTTP 200 Healthy. Data mounts and
  project remain intact. No 5032 operation or fresh-client reset was performed.

## Validity and closure limits

proof/SB11/manifest.md indexes original evidence, ownership, hashes, test selection
and limits. Image creation on -2 remains valid because its repaired source files are
unchanged on -3; the only extra production source difference is vision data validation,
which is independently exercised through the final UI run and final preview.

The selected broad Unit and Integration gates are complete and their original results
are reviewed in proof/SB11/broad-regression-results.md. Eleven unchanged baseline
failures remain; the repository is not green. No Components or solution-wide gate is
claimed because this repair changes no UI component code or project references.
Separate client 5212's expired token was not renewed as part of the 5214 incident.
Source usage completeness is proved; absent price data is reported as Unavailable.

Reopen if credential state, safe status ownership, option schema, image/text budgets,
source/consumer request behavior, file authority or deployed owner bytes change.
The completed-stage structural validator supplements, not replaces, this review.
