# CRM-HR enterprise card proposal

`enterprise-card-system-proposal.png` is the visual reference used for the CRM-HR card refresh.

The proposal was generated from the previous Directory screenshot with these constraints:

- five equal-width enterprise record cards for AI agents, people, organizations, units, and inactive records;
- one fixed anatomy: corner status, centered media, type badge, title, summary, tags, and footer;
- restrained semantic colors with a calm white/slate base;
- diagonal active/inactive status ribbons in the top-right corner;
- two-line title truncation and single-line badge truncation;
- consistent vertical baselines, spacing, borders, hover treatment, and selected state;
- dense enough for operational CRM use without looking like a consumer dashboard.

The implemented UI follows the proposal's geometry and information hierarchy while using the existing CanDoItAll component library and `TooltipService`-backed `TooltipTarget` component.
