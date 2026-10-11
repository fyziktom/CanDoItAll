# Native authority and sensitive boundaries

UI extraction does not transfer ownership of projects, files, providers, processes, workflows, business assignments or persistence. Keep one authoritative native writer for each fact. Reuse existing application contracts; do not reach through another module's DbContext or recreate its policy in presentation code.

Preserve distinctions: technical agents versus CRM/HR records; Simple Chats versus managed execution; process orchestration versus workflow execution; graph appearance versus scheduling; file bytes/roots versus metadata versus project contributions; provider-neutral contracts versus concrete drivers. Existing backend debt is neither permission for a new unsafe edge nor a demand to redesign the whole engine in a UI task.

## Context and permissions

Source-read permission, visibility in an agent context, authority to mutate, delegated capability, project admission and runtime approval are different facts. Hiding a control is not enforcement. Preserve current ProjectWriteAdmission/project binding and equivalent native lifetimes. A deleted/recreated project with the same public ID is not the old admitted target. Never mint a replacement authority token in a renderer to make a retry succeed.

Maintain redaction, HTML/Markdown sanitization, attachment/URL policies, trusted workspace roots, bounded content, platform/headless restrictions and safe public errors. Keep secrets, raw authority and private paths out of URLs, context snapshots, screenshots, console output and unrestricted UI contracts. Current denial invalidates access; a previous accepted snapshot does not override it.

## Durable effects

An accepted launch or write can outlive the component. Navigation/closing stops presentation-owned reads and subscriptions, not native work. Explicit Cancel retains its actual backend semantics. A UI busy gate is not exactly-once delivery. Restore native caller intent/preparation identity on observation/recovery; a new explicit user action is different from retrying an uncertain or accepted action.

Keep approved tool mutation, sandbox/workspace restrictions, artifact recovery, receipts and continuation/link delivery at native boundaries. Do not disable governance or inject an always-successful service to get a green UI scenario. Backend unsupported operations remain explicitly unsupported and server-side rejected.

## Scope and environment

No database schema migration, new runtime bus, global auth redesign, whole-application JWT/SSR change, model/package upgrade, real external publication or paid inference follows from this companion. A necessary bounded owner correction must be traced, separately reviewed and tested at its actual owner. A larger prerequisite gets an explicit causal report and blocked status, not an improvised architectural expansion.

Use isolated PostgreSQL and owned fixtures for durable proof. Do not reuse ordinary databases, retained provider stores, ports, signing material or installations. Current repository rules govern release/publish/signing. Demo-specific provisioning and rehearsal artifacts belong in `CanDoItAll.Demos`; shared machine tooling in its existing sibling repositories. Keep generic regression tests and architecture in this repository.
