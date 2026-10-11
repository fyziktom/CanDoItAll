using CanDoItAll.Application.UI.Profiles;
using CanDoItAll.Application.UI.Shell;

namespace CanDoItAll.Application.UiSandbox;

public enum ProfileScenario {
    Startup,
    Active,
    Selected,
    Pending,
    Locked,
    Busy,
    Error,
    Empty
}

public static class ProfileScenarios {
    public static readonly Guid PrimaryId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    public static readonly Guid SecondaryId = Guid.Parse("66666666-6666-4666-8666-666666666666");

    public static DatabaseProfileDialogPresentation Create(ProfileScenario scenario) {
        var locked = scenario == ProfileScenario.Locked;
        var active = scenario is ProfileScenario.Startup or ProfileScenario.Active or ProfileScenario.Locked;
        var pending = scenario == ProfileScenario.Pending;
        var selection = new DatabaseSelectionDisplay("Primary workspace", "PostgreSQL", "PostgreSQL connection",
            locked ? "Explicit startup override" : "Last active profile", "scenario-host / primary", "scenario workspace", locked,
            pending ? SecondaryId : null, pending ? "Review workspace" : string.Empty);
        DatabaseProfileDisplay[] profiles = scenario == ProfileScenario.Empty ? [] : [
            new(PrimaryId, "Primary workspace", "PostgreSQL", "PostgreSQL connection", "scenario-host / primary", true, false, locked),
            new(SecondaryId, "Review workspace", "PostgreSQL", "PostgreSQL connection", "scenario-host / review", false, pending, false)
        ];
        return new(Guid.NewGuid(), true, scenario == ProfileScenario.Startup, selection, profiles,
            scenario == ProfileScenario.Empty ? null : active ? PrimaryId : SecondaryId,
            scenario == ProfileScenario.Busy, active, pending, !locked && !active && !pending && profiles.Length > 0,
            scenario == ProfileScenario.Error ? new("The selected profile is unavailable. Review its settings.", DatabaseProfileMessageTone.Error) :
            pending ? new("Activation saved. Restart the process to use Review workspace.", DatabaseProfileMessageTone.RestartRequired) : null);
    }

    public static ShellUtilitiesPresentation Utilities(DatabaseProfileDialogPresentation view) => new(view.OpeningId,
        view.DatabaseSelection?.DisplayName ?? "Loading runtime profile", new("scen***", "prim***", "scenario-user"),
        "Profile: Primary workspace\nServer: scen***\nDatabase: prim***\nUser: scenario-user",
        view.DatabaseProfiles.Where(profile => !profile.IsActive).ToArray(), view.DatabaseDialogBusy,
        view.DatabaseSelection?.IsRuntimeLocked != true);
}
