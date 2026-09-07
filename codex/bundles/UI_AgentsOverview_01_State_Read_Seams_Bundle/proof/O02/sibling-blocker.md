# Confirmed bounded DialogService blocker

The frozen O02 RED run contains both query-transition cases of Leaving_overview_or_replacing_scope_closes_only_owned_usage_dialogs. Both fail semantically against real registered DialogService: its LocationChanged handler calls CloseAll, removing the unrelated reference. No page-owned token can prevent that independent subscriber from closing another owner. Page removal is a separate witness.

The owner instruction makes a sibling exception conditional on a separately proven unavoidable blocker. This condition is met. The smallest correction is an opt-in disposable same-page navigation scope; default navigation behavior is unchanged, and changes of path still close dialogs. AgentsHomePage opts in for its lifetime and explicitly cancels only its three usage dialogs on scope/tab change. Rejected alternatives: globally change navigation policy; fake navigation/JS history; private event reflection; reopen another owner's dialog after closing it.

Sibling was clean at c3e6aa03a878994c0ba8aed6af017d0be75f3796 immediately before editing. Only DialogService, direct ownership tests, and reviewed public API approval metadata may change. No assets or other sibling work is intended. FileTools stays read-only. This public API addition is a named stable-gate invalidation reason. No commit or push is authorized.
