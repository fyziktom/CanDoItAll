namespace CanDoItAll.Modules.Projects.Pages.Components;

internal static class ProjectEditorValues {
    internal static bool MatchesOriginal(ProjectEditorModel model, ProjectDefinitionSnapshot? original) {
        if (original is null) {
            return model.Id is null && Scalars(model) == new ProjectDefinitionScalars("", "", "", ProjectStatus.Draft, "", null) &&
                Identity(model) is null && model.Phases.Count == 0 && model.Options.All(row => row.Id is null &&
                    string.IsNullOrWhiteSpace(row.OptionName) && string.IsNullOrWhiteSpace(row.Notes));
        }
        if (Scalars(model) != original.Scalars || Identity(model) != original.ExternalIdentity || model.Phases.Count != original.Phases.Count) {
            return false;
        }
        for (var index = 0; index < model.Phases.Count; index++) {
            var row = model.Phases[index];
            var saved = original.Phases[index];
            if (row.Id != saved.Id || row.Name != saved.Name || row.Goal != saved.Goal || row.Status != saved.Status ||
                row.StartDateUtc != saved.StartDateUtc || row.EndDateUtc != saved.EndDateUtc) {
                return false;
            }
        }
        var options = original.Options.ToDictionary(row => row.Id);
        var seen = new HashSet<Guid>();
        foreach (var row in model.Options.Where(row => row.Id.HasValue || !string.IsNullOrWhiteSpace(row.OptionName) || !string.IsNullOrWhiteSpace(row.Notes))) {
            if (row.Id is not { } id || !seen.Add(id) || !options.TryGetValue(id, out var saved) ||
                row.Category != saved.Category || row.OptionName != saved.OptionName || row.Notes != saved.Notes) {
                return false;
            }
        }
        return seen.Count == options.Count;
    }

    internal static ProjectDefinitionScalars Scalars(ProjectEditorModel model)
        => new(model.Name, model.Description, model.Objective, model.Status, model.CurrentPhase, model.TargetDateUtc);

    internal static ProjectDefinitionExternalIdentity? Identity(ProjectEditorModel model)
        => model.ExternalNamespace is null && model.ExternalKey is null ? null : new(model.ExternalNamespace ?? "", model.ExternalKey ?? "");

    internal static void CopyScalars(ProjectEditorModel source, ProjectEditorModel target) {
        target.Id = source.Id;
        target.ExpectedLifetimeId = source.ExpectedLifetimeId;
        target.ExpectedProjectAdmission = source.ExpectedProjectAdmission;
        target.ExpectedDefinition = source.ExpectedDefinition;
        target.ExternalNamespace = source.ExternalNamespace;
        target.ExternalKey = source.ExternalKey;
        target.Name = source.Name;
        target.Description = source.Description;
        target.Objective = source.Objective;
        target.Status = source.Status;
        target.CurrentPhase = source.CurrentPhase;
        target.TargetDateUtc = source.TargetDateUtc;
    }

    internal static void Copy(ProjectPhaseEditorModel source, ProjectPhaseEditorModel target) {
        target.Id = source.Id;
        target.Name = source.Name;
        target.Goal = source.Goal;
        target.Status = source.Status;
        target.StartDateUtc = source.StartDateUtc;
        target.EndDateUtc = source.EndDateUtc;
    }

    internal static void Copy(ProjectOptionEditorModel source, ProjectOptionEditorModel target) {
        target.Id = source.Id;
        target.Category = source.Category;
        target.OptionName = source.OptionName;
        target.Notes = source.Notes;
    }

    internal static bool Same(ProjectEditorModel live, ProjectEditorSubmission captured) {
        var model = captured.Model;
        if (live.ExpectedDefinition != model.ExpectedDefinition || live.ExpectedLifetimeId != model.ExpectedLifetimeId ||
            live.ExpectedProjectAdmission != model.ExpectedProjectAdmission || live.Id != model.Id || Scalars(live) != Scalars(model) ||
            Identity(live) != Identity(model) || live.Phases.Count != model.Phases.Count || live.Options.Count != model.Options.Count) {
            return false;
        }
        for (var index = 0; index < live.Phases.Count; index++) {
            var row = live.Phases[index];
            var value = model.Phases[index];
            if (!ReferenceEquals(row, captured.PhaseRows[index]) || row.Id != value.Id || row.Name != value.Name || row.Goal != value.Goal ||
                row.Status != value.Status || row.StartDateUtc != value.StartDateUtc || row.EndDateUtc != value.EndDateUtc) {
                return false;
            }
        }
        for (var index = 0; index < live.Options.Count; index++) {
            var row = live.Options[index];
            var value = model.Options[index];
            if (!ReferenceEquals(row, captured.OptionRows[index]) || row.Id != value.Id || row.Category != value.Category ||
                row.OptionName != value.OptionName || row.Notes != value.Notes) {
                return false;
            }
        }
        return true;
    }
}
