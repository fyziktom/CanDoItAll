using System.Globalization;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Modules.Projects;

public sealed class ProjectLifecycleRecoveryCodec : IProjectLifecycleRecoveryCodec {
    private const int MaximumSubmissionBytes = 4096;
    private const string AbsenceDomain = "projects:lifecycle-closed-absence:v1";
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public ProjectLifecycleFrozenSubmission Freeze(ProjectLifecycleSubmission original)
        => new(original, Utf8.GetString(ProjectLifecycleOperationCodec.EncodeSubmission(original)));

    public ProjectLifecycleFrozenSubmission ReadSubmission(string payload)
        => new(ProjectLifecycleOperationCodec.DecodeSubmission(Bytes(payload, MaximumSubmissionBytes)), payload);

    public ProjectLifecycleReceiptPayload EncodeAccepted(ProjectLifecycleSubmission original, ProjectLifecycleAccepted accepted) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(accepted);
        if (accepted.Original != original) {
            throw new InvalidDataException("The lifecycle receipt does not acknowledge this exact original submission.");
        }
        return new(accepted, Utf8.GetString(ProjectLifecycleOperationCodec.EncodeAccepted(accepted)));
    }

    public ProjectLifecycleReceiptPayload ReadAccepted(ProjectLifecycleSubmission original, string payload) {
        ArgumentNullException.ThrowIfNull(original);
        var accepted = ProjectLifecycleOperationCodec.DecodeAccepted(Bytes(payload, ProjectLifecycleAccepted.MaximumPayloadBytes), original.Operation);
        return EncodeAccepted(original, accepted);
    }

    public ProjectLifecycleAbsencePayload EncodeClosedAbsent(ProjectLifecycleSubmission original, ProjectLifecycleLookup.ClosedAbsent absence) {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(absence);
        if (absence.Original != original.Operation || !FiniteUtc(absence.ClosedAtUtc) || !FiniteUtc(absence.ObservedAtUtc) ||
                !FiniteUtc(absence.ReplayUntilUtc) || absence.ClosedAtUtc > absence.ObservedAtUtc || absence.ClosedAtUtc >= absence.ReplayUntilUtc) {
            throw new InvalidDataException("Closed absence requires the exact original address and ordered finite server observations.");
        }
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) {
            writer.WriteStartObject();
            writer.WriteString("domain", AbsenceDomain);
            writer.WritePropertyName("original");
            writer.WriteRawValue(ProjectLifecycleOperationCodec.EncodeSubmission(original));
            writer.WriteString("closedAtUtc", absence.ClosedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("observedAtUtc", absence.ObservedAtUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString("replayUntilUtc", absence.ReplayUntilUtc.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteEndObject();
        }
        var bytes = stream.ToArray();
        if (bytes.Length > ProjectLifecycleAccepted.MaximumPayloadBytes) {
            throw new InvalidDataException("Closed absence exceeds the retained evidence bound.");
        }
        return new(absence, Utf8.GetString(bytes));
    }

    public ProjectLifecycleAbsencePayload ReadClosedAbsent(ProjectLifecycleSubmission original, string payload) {
        ArgumentNullException.ThrowIfNull(original);
        try {
            using var document = JsonDocument.Parse(Bytes(payload, ProjectLifecycleAccepted.MaximumPayloadBytes), new() { MaxDepth = 10 });
            var root = document.RootElement;
            var retained = ProjectLifecycleOperationCodec.DecodeSubmission(Utf8.GetBytes(root.GetProperty("original").GetRawText()));
            if (root.GetProperty("domain").GetString() != AbsenceDomain || retained != original) {
                throw new InvalidDataException("Closed absence belongs to another original submission.");
            }
            var result = EncodeClosedAbsent(original, new(original.Operation, root.GetProperty("closedAtUtc").GetDateTimeOffset(),
                root.GetProperty("observedAtUtc").GetDateTimeOffset(), root.GetProperty("replayUntilUtc").GetDateTimeOffset()));
            if (!string.Equals(result.Payload, payload, StringComparison.Ordinal)) {
                throw new InvalidDataException("Closed absence is not its exact canonical representation.");
            }
            return result;
        } catch (Exception failure) when (failure is JsonException or ArgumentException or InvalidOperationException or FormatException or KeyNotFoundException) {
            throw new InvalidDataException("The retained closed-absence evidence is invalid.", failure);
        }
    }

    private static byte[] Bytes(string payload, int maximum) {
        ArgumentNullException.ThrowIfNull(payload);
        if (payload.Length is 0 || payload.Length > maximum || Utf8.GetByteCount(payload) > maximum) {
            throw new InvalidDataException("The retained lifecycle evidence is outside its byte bound.");
        }
        return Utf8.GetBytes(payload);
    }

    private static bool FiniteUtc(DateTimeOffset value)
        => value.Offset == TimeSpan.Zero && value != DateTimeOffset.MinValue && value != DateTimeOffset.MaxValue;
}
