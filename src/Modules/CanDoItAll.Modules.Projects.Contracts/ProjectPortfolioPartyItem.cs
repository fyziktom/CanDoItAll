namespace CanDoItAll.Modules.Projects;

/// <summary>
/// Group of a project participant in project summaries, as a JSON integer: 0 Customer, 1 DeliveryUnit, 2 Owner
/// (participating managers, team members and reviewers), 3 Stakeholder (every other participation role), 4 Partner,
/// 5 AiAgent.
/// </summary>
public enum ProjectPartyPortfolioCategory
{
    Customer,
    DeliveryUnit,
    Owner,
    Stakeholder,
    Partner,
    AiAgent
}

/// <summary>
/// A party that participates in a project as a whole, as listed in a project summary.
/// </summary>
/// <param name="Category">
/// Group of the participant, as a JSON integer: 0 Customer, 1 DeliveryUnit, 2 Owner, 3 Stakeholder, 4 Partner,
/// 5 AiAgent.
/// </param>
/// <param name="Label">
/// English label of the participation role, for example <c>Customer contact</c>, for display.
/// </param>
/// <param name="DisplayName">Display name of the participating party.</param>
/// <param name="IsPrimary">True when the participation is marked as primary.</param>
public sealed record ProjectPortfolioPartyItem(
    ProjectPartyPortfolioCategory Category,
    string Label,
    string DisplayName,
    bool IsPrimary);
