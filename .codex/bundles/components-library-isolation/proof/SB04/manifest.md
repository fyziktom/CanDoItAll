# SB04 Proof Manifest

## Changed File Hashes

- `repo://CanDoItAll.slnx`: 2B20D51221149511CA88784232B09B794E9527A362446EC851E0F0EB337E0730
- `repo://CanDoItAll.Space3D.slnx`: 64F01E7FD5C086817C5B070ED1296F486C7E60EECFD5D3244A2C6F4A336695AD

## Source Proof

- `repo://CanDoItAll.slnx` excludes moved component projects and Space3D projects.
- `repo://CanDoItAll.Space3D.slnx` contains the Space3D projects.
- `repo://tests/CanDoItAll.Space3D.Tests/CanDoItAll.Space3D.Tests.csproj` owns Space3D tests outside the main test project.
- `repo://src/CanDoItAll.Web/Components/App.razor` loads package CSS and main app CSS for browser validation.
- Semantic invariant contract: `bundle://proof/SB04/semantic-invariants.md`.

## Command Proof

- Passing transcript: `bundle://proof/SB04/transcripts/sb04-closure-proof.txt`
- Semantic positive proof: `bundle://proof/SB04/transcripts/sb04-closure-proof.txt`
- Anti-stub audit: `bundle://proof/SB04/transcripts/sb04-closure-proof.txt`
- Adversarial negative proof: N/A process/no production behavior exemption; the final negative check is the source-membership audit summarized in the passing transcript.

## Browser Proof

- Snapshot: `bundle://proof/SB04/browser-home-smoke.md`
- Screenshot: `bundle://proof/SB04/browser-home-smoke.png`
