# Mermaid gantt

The chart below is a dependency-oriented execution map, not a fixed contractual schedule. Dates are illustrative and start from the week after bundle creation.

```mermaid
gantt
    title CanDoItAll CRM/HR implementation waves
    dateFormat  YYYY-MM-DD
    axisFormat  %m/%d

    section Wave A - Foundation
    B01 Foundation                           :done, b01, 2026-03-30, 8d
    B02 Shell + core pages                   :after b01, 5d

    section Wave B - Shared identities
    B03 Relationships + dedupe               :after b02, 6d
    B06 Workforce profiles + units           :after b02, 6d
    B09 AI agents + provider bindings        :after b02, 5d

    section Wave C - Project-aware integration
    B10 Project/Workbench assignments        :after b03, 8d
    B04 CRM accounts + interactions          :after b03, 6d

    section Wave D - Commercial and staffing depth
    B05 Opportunities + project conversion   :after b10, 6d
    B07 Skills + capacity + allocations      :after b10, 7d
    B08 Recruiting + onboarding/offboarding  :after b06, 6d

    section Wave E - Hardening and release
    B11 Cross-module integration             :after b08, 6d
    B12 Privacy + audit + lifecycle          :after b11, 4d
    B13 Regression + rollout                 :after b12, 6d
```

## Reading notes

- B03, B06, and B09 can proceed in parallel once the module shell and base schema exist.
- B10 is the pivotal bridge into Projects and Workbench.
- B05 and B07 intentionally wait for B10 because both need stable project-assignment semantics.
- B13 is a real work package with code, tests, evidence conventions, and rollout rehearsal.
