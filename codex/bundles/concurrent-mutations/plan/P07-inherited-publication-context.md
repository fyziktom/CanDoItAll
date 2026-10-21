# Inherited publication context — reviewed correction

The actual produced Project child reaches the saved publisher, but both RunSnapshot
and StepSnapshot positive cases return SourceChanged. Earlier manually prepared
publication fixtures used the organization scope for both storage and execution.

AgentPlanningPublicationExpectation.Scope identifies the organization catalog and
run store. Its constructor deliberately rejects other scopes. In contrast,
AgentPlanningRunMetadata.ReadScope identifies the original execution context, which
can be a Project. AgentPlanningDecisionOwner.RequirePublication incorrectly compares
these two scopes, rejecting valid produced children.

Remove that comparison. Preserve the earlier exact MetadataJson comparison between
the held run and the live work's original run; the execution scope is part of those
bytes. Preserve the original session/profile, inherited origin, bound claim, selected
proposal, journal and budget checks. The catalog lease continues requiring the exact
organization. There is no new authority, dependency, interface, lock or query.

Extend the independent inherited saved-effect tests to use both organization and
Project execution contexts in the same organization store, for dispatch and manager
review. Tamper with the original context after claiming and require refusal. Retain
the actual produced-child cases in both snapshot modes and the interactive saved-action
regressions. Rebuild Core and refresh both test assemblies; the earlier binary bridge
cannot qualify a changed Core binary. Repeat the 209 review unit cases along with the
affected integration lanes, static enforcement and resource reconciliation.

Core baseline snap-20261021103047-8e94da1d already includes this original owner.
The correction removes a faulty condition from that owner without adding a member
or partial. Scoped structural evidence retains existing collector limitations.
