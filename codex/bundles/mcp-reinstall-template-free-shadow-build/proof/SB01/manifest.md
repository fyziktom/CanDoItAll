# SB01 Proof Manifest

## Changed File Hashes

- SHA-256 `c23286bad616de1dd5bbcd4c1cf7872e2bcead3db1927c1dd3e79fa563a399ec` - `repo://Directory.Build.targets`
- SHA-256 `3acf52c27f90fe15f64655c8ad9e664e84f66067b9a91950d69207017317c645` - `repo://tools/Reinstall-CanDoItAllMcps.ps1`
- SHA-256 `1ab837a898c50b6914f05fbd8662907ff0bb076b799ce5990a1cd1619306150b` - `repo://tools/CanDoItAll.Mcp.DotNetWatch/Start-CanDoItAllDotNetWatchMcp.ps1`

## Command Transcripts

- Failing-first transcript: `bundle://proof/SB01/transcripts/failing-first-current-state.txt`
- Passing transcript: `bundle://proof/SB01/transcripts/reinstall-pass.txt`
- Semantic positive proof transcript: `bundle://proof/SB01/transcripts/artifact-scan.txt`
- Anti-stub audit transcript: `bundle://proof/SB01/transcripts/anti-stub-audit.txt`
- Changed-file hash transcript: `bundle://proof/SB01/transcripts/changed-file-hashes.txt`

## Source Assertions

- Source assertion artifact: `bundle://proof/SB01/source-assertions.md`
- Semantic invariant contract: `bundle://proof/SB01/semantic-invariants.md`
- Final verifier artifact: `bundle://proof/SB01/verifier.md`

## Semantic Adequacy Gate

- Raw note owned: NOTE-001 through NOTE-007 are owned by SB01.
- Shallow-pass trap: a fix that only shortens the shadow hash or only passes a targeted build would not satisfy the full reinstall transcript plus artifact scan.
- Adversarial negative proof: `bundle://proof/SB01/transcripts/failing-first-current-state.txt` exits non-zero and shows the old template-copy long-path failure.
- Semantic positive proof: `bundle://proof/SB01/transcripts/reinstall-pass.txt` exits zero for the full MCP reinstall, and `bundle://proof/SB01/transcripts/artifact-scan.txt` exits zero with no copied `Templates` directories in current MCP artifacts.
- Anti-stub audit: `bundle://proof/SB01/transcripts/anti-stub-audit.txt` reports no stub, TODO, fixture-only, hard-coded success, or fake-proof markers.
- Raw-note literal closure: `bundle://proof/SB01/verifier.md` closes the request without moving templates or removing skill sync.
