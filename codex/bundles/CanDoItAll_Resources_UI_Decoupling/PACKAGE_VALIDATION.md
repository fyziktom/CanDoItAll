# Package validation receipt

This receipt concerns the handoff archive only, not the CanDoItAll product.

The package contains the English executable prompt, bounded Memory review, complete
Resources source/design notes, validation matrix, development-loop instructions, explicit
proof limitations, source register, metadata and portable integrity tools.
All 22 inherited shared-v3 files are preserved byte-for-byte from the supplied Memory
handoff. Their own historical audit and integrity manifest are not rewritten.

## Executed local results

Root validator: passed for 36 files, five JSON files, 42 source entries, 52 local links and
35 root manifest entries. All 11 root validator tests and all 14 inherited shared-tool tests
passed, with no failed or skipped tests. The shared validator passed, and all 22 shared
files matched the previous Memory handoff byte-for-byte. The final archive was also checked
for ZIP integrity and validated from a new extracted copy.

These counts do not certify C# implementation, runtime behavior or external link contents.

## Checks

The final packaging process validates UTF-8 text/JSON, declared metadata paths, source
ID uniqueness and pinned repository-source links, local Markdown destination existence,
complete SHA-256 manifest inventory, ZIP CRC/integrity and a freshly extracted archive.
Root and shared tool tests run separately, using temporary test copies. Their outputs
are recorded in the review workspace, outside the sealed archive.

The root validator does not validate Markdown anchors or external URL reachability,
independently prove a source's authenticity, compile C#, or execute product behavior.
No product build/test/browser/watch result is asserted by this receipt. See
[proof status](PROOF_STATUS.md) and [source coverage](SOURCES.md).

Commands from the extracted package root:

```powershell
python tools/validate_package.py
python tools/test_package.py
python shared/tools/validate_bundle.py
python shared/tools/test_tooling.py
```

Do not edit the sealed manifest to hide drift. Record current checkout changes separately
and adapt the implementation scope; the review commit is not an execution pin.
