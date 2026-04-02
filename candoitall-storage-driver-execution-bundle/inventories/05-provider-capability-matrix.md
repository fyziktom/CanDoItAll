
# Provider Capability Matrix

| Capability | FileSystem | IPFS | FTP | Future-provider contract | Notes |
| --- | --- | --- | --- | --- | --- |
| Read bytes/stream | Yes | Yes | Yes | Required | Shared baseline for preview/download. |
| Write bytes/stream | Yes | Yes | Yes | Required | Use streaming API, not only whole-byte arrays. |
| Delete | Yes | Optional / pin-management dependent | Yes | Optional | Capability must be advertised, not assumed. |
| List objects under prefix | Yes | Yes (via MFS or CID walk if supported) | Yes | Optional but recommended | Needed for migration and browsing. |
| Mutable in-place update | Yes | No for immutable CID; possible only via new object write | Yes | Capability flag | Recommendation engine uses this. |
| Local open path | Yes for trusted local providers | No | No | Optional | Host open button must be gated. |
| Inline preview URL | Yes via app route | Yes via app proxy or gateway redirect | Limited via app proxy | Optional | UI should not assume direct public URL. |
| Connection test | Path existence / write probe | API health + pin/add/get probe | Login + directory probe | Required | Wizard must expose it. |
| Batch folder upload | Yes | Yes | Yes | Recommended | Backed by transfer pipeline. |
| Checksum/hash reporting | File hash | CID/content hash | Optional | Recommended | Needed for verification. |
| Public/shareable address | Local app route | CID / gateway URL | Usually no | Optional | Use unified access descriptor. |
| Range read / streaming | Yes | Yes via gateway/API | Yes if client supports | Recommended | Important for videos/large media. |

## Design rule

- Capabilities are runtime facts. UI and routing decisions must come from them; they must not be re-encoded separately inside each module.
