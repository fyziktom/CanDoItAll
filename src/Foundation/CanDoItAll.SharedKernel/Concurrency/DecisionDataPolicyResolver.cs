namespace CanDoItAll.SharedKernel;

public static class DecisionDataPolicyResolver {
    public static DecisionDataPolicy Resolve(DecisionDataPolicy defaults, DecisionDataPolicyCapabilities owner,
        DecisionDataPolicyOverride? workspace = null, DecisionDataPolicyOverride? definition = null,
        DecisionDataPolicyOverride? step = null, DecisionDataPolicyOverride? authorizedInvocation = null,
        IReadOnlyList<DecisionDataPolicyBudget>? enclosingBudgets = null) {
        ArgumentNullException.ThrowIfNull(defaults);
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(owner.BudgetCeiling);
        DecisionDataPolicy.RequireDefined(owner.MinimumPublicationFreshness);
        var read = defaults.ReadBasisMode;
        var freshness = defaults.PublicationFreshness;
        var change = defaults.OnBasisChange;
        var budget = defaults.Budget;
        foreach (var preference in new[] { workspace, definition, step, authorizedInvocation }) {
            if (preference is null) {
                continue;
            }
            read = preference.ReadBasisMode ?? read;
            freshness = preference.PublicationFreshness ?? freshness;
            change = preference.OnBasisChange ?? change;
            DecisionDataPolicy.RequireDefined(read);
            DecisionDataPolicy.RequireDefined(freshness);
            DecisionDataPolicy.RequireDefined(change);
            budget = new(preference.MaximumReplans ?? budget.MaximumReplans,
                preference.MaximumWorkDuration ?? budget.MaximumWorkDuration,
                preference.MaximumBasisAge ?? budget.MaximumBasisAge);
        }
        if (!(read == ReadBasisMode.RunSnapshot ? owner.SupportsRunSnapshot : owner.SupportsStepSnapshot)) {
            throw new DecisionDataPolicyException(DecisionDataPolicyError.UnsupportedReadBasis,
                "The owner does not support the requested decision read basis.");
        }
        if (owner.MinimumPublicationFreshness == PublicationFreshness.RequireCurrent && freshness != PublicationFreshness.RequireCurrent) {
            throw new DecisionDataPolicyException(DecisionDataPolicyError.OwnerMandate,
                "A requested policy cannot relax the owner's current-publication requirement.");
        }
        if (!(freshness == PublicationFreshness.RequireCurrent ? owner.SupportsCurrentPublication : owner.SupportsPinnedObservation)) {
            throw new DecisionDataPolicyException(DecisionDataPolicyError.UnsupportedPublication,
                "The owner does not support the requested publication freshness contract.");
        }
        if (change == OnBasisChange.PauseForReview && !owner.SupportsPauseForReview ||
            change == OnBasisChange.Replan && !owner.SupportsReplan) {
            throw new DecisionDataPolicyException(DecisionDataPolicyError.UnsupportedChangeResponse,
                "The execution host does not support the requested durable change response.");
        }
        budget = Intersect(budget, owner.BudgetCeiling);
        foreach (var ceiling in enclosingBudgets ?? []) {
            ArgumentNullException.ThrowIfNull(ceiling);
            budget = Intersect(budget, ceiling);
        }
        return new(read, freshness, change, budget);
    }

    private static DecisionDataPolicyBudget Intersect(DecisionDataPolicyBudget requested, DecisionDataPolicyBudget ceiling)
        => new(Math.Min(requested.MaximumReplans, ceiling.MaximumReplans),
            requested.MaximumWorkDuration <= ceiling.MaximumWorkDuration ? requested.MaximumWorkDuration : ceiling.MaximumWorkDuration,
            requested.MaximumBasisAge <= ceiling.MaximumBasisAge ? requested.MaximumBasisAge : ceiling.MaximumBasisAge);
}
