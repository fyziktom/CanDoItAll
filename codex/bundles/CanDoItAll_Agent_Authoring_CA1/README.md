# Agent authoring CA1 + team administration

A larger, staged continuation of UI decoupling: finish the actual capability-definition
wizard and editor, then the actual technical-agent team editors, followed by native
application validation. This is not a new runtime, API-only migration, or another PP3 rewrite.

**Start with [prompt.md](prompt.md).** The [scope](SCOPE_AND_ARCHITECTURE.md),
[source review](CAPABILITY_SOURCE_REVIEW.md), [team review](TEAMS_SOURCE_REVIEW.md),
[owner safeguards](STATE_AND_OWNER_CONTRACTS.md), [validation matrix](VALIDATION_MATRIX.md)
and [application journeys](APPLICATION_JOURNEYS.md) define the work. The brief
[PP3 follow-up](S0_CARRY_OVER.md) precedes it. See [Czech review](REVIEW.cs.md).

Current repository guidance is authoritative. The unchanged [shared v3](shared/README.md)
is an execution companion. Its old module census is historical; this package's
[roadmap](ROADMAP.md) and current repository sources identify remaining work.

The reviewed main revision is b3aec979eae708edd0a53b42cef41b9d52fbb49b on
components-decoupling. It is evidence provenance, NOT an execution checkout.
The claimed tested Tooltip fix is not yet available through the connected GitHub at review
closure; see [dependency delivery](DEPENDENCIES_AND_DELIVERY.md). Do not implement it twice.

This package contains no product patch, executable test results, private logs, credentials or
font files. Product groups begin NOT_RUN. [Package checks](PACKAGE_VALIDATION.md) do not certify
the application. Keep actual evidence outside this immutable package and historical bundles.
