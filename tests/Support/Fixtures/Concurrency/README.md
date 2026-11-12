# Legacy Process prepared-launch evidence

`ProcessPreparedLaunchLegacy.json` is the original persisted entity emitted by
`ProcessPreparedLaunchCodec.ToEntity(ProcessPreparedLaunchFixture.Create())`, captured
from the compiled predecessor before Process policy source edits on 2026-10-19.
The current source checkpoint was `623686202`. No current serializer generated this
fixture after the new assignment property was introduced.

- Original Persistence assembly SHA-256: `EE0874BA5F129410944B4EE5E9A108A078EF652B41B2B5C31008F8B56A2CFF52`.
- Fixture SHA-256: `FE2B5F1A49D31982BDFF31AB180140CD78F99D0F2BFE913866CC37272C971F11`.
- Original preparation fingerprint: `sha256:fdc5c26cb66ba10c3b0156c67f36d8acb4d3e63ef9a3d474fa93f29592893567`.

The unit test decodes this entity, verifies its original hash, serializes its preparation
back to the exact saved payload and validates the original initial commit. Keep the
fixture unchanged when adding future optional fields; it is historical evidence.
