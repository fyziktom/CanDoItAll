# SB03 Proof Manifest

## Changed File Hashes

- `repo://Tailwind/input.css`: B7BBDCC809FFC6C0B95AC5650541BE3AEC359AA0E088E2C7527D92BDB105934D

## Source Proof

- `repo://Tailwind/input.css` imports only CanDoItAll-specific CSS after the split.
- `repo://Tailwind/main/tunable-boundary.css` owns the main-only tunable boundary styles.
- `repo://src/CanDoItAll.Web/Components/App.razor` loads component package CSS before main app CSS.
- Components repo Tailwind workspace owns shared component CSS and builds the BaseLib static web asset output.

## Command Proof

- Passing transcript: `bundle://proof/SB03/transcripts/sb03-closure-proof.txt`
- Anti-stub audit: `bundle://proof/SB03/transcripts/sb03-closure-proof.txt`
- Failing-first: N/A process/no production behavior exemption; this phase separates style ownership and static asset wiring.

## Documentation Proof

- `repo://README.md`
- `repo://Tailwind/README.md`
- `repo://docs/ui-shared-components/README.md`
- `repo://docs/ui-shared-components/architecture/stack-and-architecture.md`
- `repo://docs/ui-shared-components/guidelines/codex-usage-guide.md`
