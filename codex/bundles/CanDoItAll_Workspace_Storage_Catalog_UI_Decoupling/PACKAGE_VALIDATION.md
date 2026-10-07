# Handoff package validation

Run `python tools/validate_package.py` from this directory, plus the tool tests and shared
validator. This utility checks metadata, allowed text file types, local links, source-ID
references, commit-pinned repository URLs, SHA-256 manifest inventory and bytes. It rejects
unsafe paths and symlinks. It does not access GitHub, certify external URL contents, check
Markdown anchors, build C#, evaluate project graphs or execute product/browser tests.

The shared directory is copied unchanged from the provided API handoff. Its audit/map remain
historical, so WORKSPACE_STATUS.md and the actual checkout determine the current queue.
No prior module package is recursively nested. No binaries, fonts or credentials are included.

The delivery ZIP is tested for CRC integrity, unpacked into a fresh directory and validated
there. The standalone prompt and Czech summary are convenience outputs; the complete ZIP is
the executable handoff. The SHA-256 sidecar identifies the delivered archive.
