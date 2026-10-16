using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

internal sealed record ProjectDefinitionRows(
    IReadOnlyList<ProjectDefinitionPhaseInput> Phases,
    IReadOnlyList<ProjectDefinitionOptionInput> Options);

internal static class ProjectDefinitionRules {
    internal static Result<ProjectDefinitionScalars> Normalize(ProjectDefinitionScalars scalars) {
        ArgumentNullException.ThrowIfNull(scalars);
        if (string.IsNullOrWhiteSpace(scalars.Name)) {
            return Result<ProjectDefinitionScalars>.Failure(Error.Validation("Project name is required."));
        }
        if (!Enum.IsDefined(scalars.Status)) {
            return Result<ProjectDefinitionScalars>.Failure(Error.Validation("Project status is invalid."));
        }
        var normalized = scalars with {
            Name = scalars.Name.Trim(),
            Description = scalars.Description?.Trim() ?? string.Empty,
            Objective = scalars.Objective?.Trim() ?? string.Empty,
            CurrentPhase = scalars.CurrentPhase?.Trim() ?? string.Empty,
            TargetDateUtc = NormalizeDate(scalars.TargetDateUtc)
        };
        if (normalized.Name.Length > 200 || normalized.CurrentPhase.Length > 120) {
            return Result<ProjectDefinitionScalars>.Failure(Error.Validation(
                "Project name or current phase exceeds its supported field length.", ProjectErrorCodes.DefinitionLimitExceeded));
        }
        return Result<ProjectDefinitionScalars>.Success(normalized);
    }

    internal static Result<ProjectDefinitionRows> Normalize(IReadOnlyList<ProjectDefinitionPhaseInput> phases,
        IReadOnlyList<ProjectDefinitionOptionInput> options, bool creating) {
        ArgumentNullException.ThrowIfNull(phases);
        ArgumentNullException.ThrowIfNull(options);
        var phaseIds = new HashSet<Guid>();
        var acceptedPhases = new ProjectDefinitionPhaseInput[phases.Count];
        for (var index = 0; index < phases.Count; index++) {
            var phase = phases[index];
            if (phase is null || !AcceptId(phase.Id, phaseIds, creating)) {
                return Result<ProjectDefinitionRows>.Failure(InvalidChild());
            }
            if (!Enum.IsDefined(phase.Status)) {
                return Result<ProjectDefinitionRows>.Failure(Error.Validation("Project phase status is invalid."));
            }
            acceptedPhases[index] = phase with {
                Name = phase.Name?.Trim() ?? string.Empty,
                Goal = phase.Goal?.Trim() ?? string.Empty,
                StartDateUtc = NormalizeDate(phase.StartDateUtc),
                EndDateUtc = NormalizeDate(phase.EndDateUtc)
            };
            if (acceptedPhases[index].Name.Length > 160) {
                return Result<ProjectDefinitionRows>.Failure(Error.Validation(
                    "A project phase name exceeds its supported field length.", ProjectErrorCodes.DefinitionLimitExceeded));
            }
        }
        var optionIds = new HashSet<Guid>();
        var acceptedOptions = new ProjectDefinitionOptionInput[options.Count];
        for (var index = 0; index < options.Count; index++) {
            var option = options[index];
            if (option is null || !AcceptId(option.Id, optionIds, creating)) {
                return Result<ProjectDefinitionRows>.Failure(InvalidChild());
            }
            if (!Enum.IsDefined(option.Category)) {
                return Result<ProjectDefinitionRows>.Failure(Error.Validation("Project option category is invalid."));
            }
            acceptedOptions[index] = option with {
                OptionName = option.OptionName?.Trim() ?? string.Empty,
                Notes = option.Notes?.Trim() ?? string.Empty
            };
            if (acceptedOptions[index].OptionName.Length > 200) {
                return Result<ProjectDefinitionRows>.Failure(Error.Validation(
                    "A project option name exceeds its supported field length.", ProjectErrorCodes.DefinitionLimitExceeded));
            }
        }
        return Result<ProjectDefinitionRows>.Success(new(Array.AsReadOnly(acceptedPhases), Array.AsReadOnly(acceptedOptions)));
    }

    internal static bool IsAbsent(ProjectDefinitionOptionInput option) => option.OptionName.Length == 0 && option.Notes.Length == 0;

    internal static Error InvalidChild() => Error.Validation(
        "A persisted project child identifier is empty, duplicated, unknown or belongs to another project.", ProjectErrorCodes.DefinitionChildInvalid);

    internal static DateTime? NormalizeDate(DateTime? value) => value?.Kind switch {
        null => null,
        DateTimeKind.Utc => value.Value,
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
    };

    private static bool AcceptId(Guid? id, HashSet<Guid> ids, bool creating)
        => !id.HasValue || !creating && id.Value != Guid.Empty && ids.Add(id.Value);
}
