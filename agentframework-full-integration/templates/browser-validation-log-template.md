# Browser Validation Log Template

| Route | Viewport | Playwright actions | Assertions | Screenshot path | Visual findings | Result |
| --- | --- | --- | --- | --- | --- | --- |
| `/example` | `1600x900` | `navigate -> click -> assert` | `badge visible` | `artifacts/example.png` | `No clipping, action hierarchy clear` | `Pass` |

## Review Questions

- Was the main task obvious?
- Was anything clipped, overlapped or duplicated?
- Were badges / statuses / unread indicators understandable?
- Did the screen preserve process/project/agent context?
