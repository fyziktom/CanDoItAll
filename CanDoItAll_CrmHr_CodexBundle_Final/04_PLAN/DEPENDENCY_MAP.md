# Dependency map

## Bundle dependency graph

```mermaid
graph TD
    B01[B01 Foundation: unified party domain, schema, and module skeleton]
    B02[B02 Directory shell, navigation, routes, and core BaseLib pages]
    B03[B03 Contact points, addresses, relationships, org structure, import/export, and duplicate merge]
    B04[B04 CRM accounts, contacts, stakeholders, interaction journal, and follow-ups]
    B05[B05 Opportunities, pipeline, stage history, and project conversion]
    B06[B06 HR workforce structure, worker profiles, and delivery units]
    B07[B07 Skills, capacity, staffing requests, bench management, and allocations]
    B08[B08 Recruitment pipeline, interviews, onboarding, and offboarding]
    B09[B09 AI agent profiles, provider bindings, capabilities, and governance]
    B10[B10 Project and Workbench party assignment integration]
    B11[B11 Cross-module integration with search, activity, resources, validation, test lab, and automation]
    B12[B12 Security, privacy, audit, and safe lifecycle controls]
    B13[B13 Validation hardening, rollout, migration rehearsal, and regression suite]
    B01 --> B02
    B01 --> B03
    B02 --> B03
    B01 --> B04
    B02 --> B04
    B03 --> B04
    B01 --> B05
    B02 --> B05
    B03 --> B05
    B04 --> B05
    B10 --> B05
    B01 --> B06
    B02 --> B06
    B03 --> B06
    B01 --> B07
    B02 --> B07
    B03 --> B07
    B06 --> B07
    B10 --> B07
    B01 --> B08
    B02 --> B08
    B03 --> B08
    B06 --> B08
    B01 --> B09
    B02 --> B09
    B03 --> B09
    B01 --> B10
    B02 --> B10
    B03 --> B10
    B06 --> B10
    B09 --> B10
    B01 --> B11
    B02 --> B11
    B03 --> B11
    B04 --> B11
    B05 --> B11
    B06 --> B11
    B07 --> B11
    B08 --> B11
    B09 --> B11
    B10 --> B11
    B01 --> B12
    B02 --> B12
    B03 --> B12
    B04 --> B12
    B06 --> B12
    B08 --> B12
    B11 --> B12
    B01 --> B13
    B02 --> B13
    B03 --> B13
    B04 --> B13
    B05 --> B13
    B06 --> B13
    B07 --> B13
    B08 --> B13
    B09 --> B13
    B10 --> B13
    B11 --> B13
    B12 --> B13
```

## Dependency table

| Bundle | Depends on | Blocks |
| --- | --- | --- |
| B01 | None | B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13 |
| B02 | B01 | B03, B04, B05, B06, B07, B08, B09, B10, B11, B12, B13 |
| B03 | B01, B02 | B04, B05, B06, B07, B08, B09, B10, B11, B12, B13 |
| B04 | B01, B02, B03 | B05, B11, B12, B13 |
| B05 | B01, B02, B03, B04, B10 | B11, B13 |
| B06 | B01, B02, B03 | B07, B08, B10, B11, B12, B13 |
| B07 | B01, B02, B03, B06, B10 | B11, B13 |
| B08 | B01, B02, B03, B06 | B11, B12, B13 |
| B09 | B01, B02, B03 | B10, B11, B13 |
| B10 | B01, B02, B03, B06, B09 | B05, B07, B11, B13 |
| B11 | B01, B02, B03, B04, B05, B06, B07, B08, B09, B10 | B12, B13 |
| B12 | B01, B02, B03, B04, B06, B08, B11 | B13 |
| B13 | B01, B02, B03, B04, B05, B06, B07, B08, B09, B10, B11, B12 | None |


## Critical path

The most important dependency chain is:

`B01 -> B02 -> B03 -> B10 -> B05 -> B11 -> B12 -> B13`

Why:

- B01/B02 create the module and route surface.
- B03 makes the directory actually usable.
- B10 integrates the new identity model with Projects and Workbench.
- B05 depends on real project conversion semantics.
- B11/B12/B13 complete cross-module wiring, privacy hardening, and final proof.

## Parallelization guidance

Safe parallel groups:

- `B03`, `B06`, `B09`
- later `B05`, `B07`, `B08` after their dependencies are satisfied

Avoid parallelizing:

- B10 before B03/B06/B09 are stable
- B13 before B11/B12 are actually done
- privacy/audit policy before the main entities exist
