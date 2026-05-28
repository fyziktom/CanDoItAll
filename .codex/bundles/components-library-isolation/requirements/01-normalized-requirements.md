# Normalized Requirements

| ID | Requirement | Source | Owning Subbundle | Proof |
| --- | --- | --- | --- | --- |
| REQ-001 | Move exactly the eight requested component projects into `C:/repositories/CanDoItAll.Components` and remove those folders from the main repo. | `bundle://inputs/00-original-request.md` | SB01 | Components repo inventory, main repo absence check, components solution build. |
| REQ-002 | Create a new solution in the components repo containing the moved projects. | `bundle://inputs/00-original-request.md` | SB01 | `CanDoItAll.Components.slnx` source assertion and build transcript. |
| REQ-003 | Make every moved project build as NuGet package version `0.1.0` with README/package information. | `bundle://inputs/00-original-request.md` | SB01 | Pack transcript and package metadata/source assertions. |
| REQ-004 | Add built private packages to `repo://ExternalPackages` in the main repo. | `bundle://inputs/00-original-request.md` | SB02 | Package file inventory and main restore transcript. |
| REQ-005 | Replace main-repo direct project references to moved components with package references. | `bundle://inputs/00-original-request.md` | SB02 | Project reference audit and main build transcript. |
| REQ-006 | Keep `CanDoItAll.Components` and `CanDoItAll.Components.WebGlSandbox` in the main repo but consume moved dependencies as packages. | `bundle://inputs/00-original-request.md` | SB02 | Source assertion and targeted project builds. |
| REQ-007 | Split Tailwind into component-library output and CanDoItAll-specific output, with instructions in both repos. | `bundle://inputs/00-original-request.md` | SB03 | Tailwind build transcripts, app CSS link source assertion, docs. |
| REQ-008 | Remove Space3D projects from main slnx and add a separate Space3D slnx. | `bundle://inputs/00-original-request.md` | SB04 | Slnx source assertion and build command. |
| REQ-009 | Update documentation for the new components repo and the main repo package consumption model. | `bundle://inputs/00-original-request.md` | SB03 | Docs source assertion. |
| REQ-010 | Validate that behavior still works at build/test level and record any browser/runtime blocker explicitly. | `bundle://inputs/00-original-request.md` | SB04 | Build/test transcripts and browser analytics rows. |
