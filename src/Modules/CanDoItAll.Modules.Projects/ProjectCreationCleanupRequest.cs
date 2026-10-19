using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Projects;

public sealed record ProjectCreationCompensationRequest {
    private ProjectCreationCompensationRequest(ProjectCreationReceipt receipt, ProjectDeletionPreparationContext original) {
        Receipt = receipt;
        Original = original;
    }

    public ProjectCreationReceipt Receipt { get; }
    public ProjectDeletionPreparationContext Original { get; }

    public static ProjectCreationCompensationRequest FromReceipt(ProjectCreationReceipt receipt) {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(receipt.Project);
        ArgumentNullException.ThrowIfNull(receipt.Namespace);
        _ = new ProjectDeletionOriginEvidence.Compensation(receipt.Project, receipt.Fingerprint, receipt.Reservation);
        return new(receipt, new(new(receipt.Project.DatabaseProfileId, receipt.Namespace,
            ProjectCreationCleanupIdentity.Compensation(receipt)), receipt.Project));
    }
}

public sealed record ProjectReservationCancellationRequest {
    internal ProjectReservationCancellationRequest(DatasetNamespaceId originalNamespace, ProjectCreationReservation reservation) {
        ArgumentNullException.ThrowIfNull(originalNamespace);
        ArgumentNullException.ThrowIfNull(reservation);
        ProjectCreationCleanupIdentity.RequireReservation(reservation);
        Reservation = reservation;
        Original = new(new(reservation.DatabaseProfileId, originalNamespace,
            ProjectCreationCleanupIdentity.Reservation(originalNamespace, reservation)),
            new(reservation.DatabaseProfileId, reservation.ProjectId, reservation.LifetimeId));
    }

    public ProjectCreationReservation Reservation { get; }
    public ProjectDeletionPreparationContext Original { get; }
    public ProjectReservationCancellationPreparationContext Preparation => new(Original,
        new(Original.Project, Reservation.Id, Reservation.RequesterId, Reservation.ParentProjectId is { } parent
            ? new(Reservation.DatabaseProfileId, parent, Reservation.ParentLifetimeId!.Value) : null));
}

internal static class ProjectCreationCleanupIdentity {
    private const string CompensationDomain = "projects:creation-compensation:v1";
    private const string ReservationDomain = "projects:reservation-cancellation:v1";

    internal static ProjectDeletionCompletionId Compensation(ProjectCreationReceipt receipt)
        => Create(CompensationDomain, receipt.Namespace, receipt.Project, receipt.Fingerprint, receipt.Reservation?.Id);

    internal static ProjectDeletionCompletionId Reservation(DatasetNamespaceId originalNamespace, ProjectCreationReservation reservation)
        => Create(ReservationDomain, originalNamespace, new(reservation.DatabaseProfileId, reservation.ProjectId,
            reservation.LifetimeId), null, reservation.Id);

    internal static void RequireReservation(ProjectCreationReservation value) {
        if (value.Id == Guid.Empty || value.DatabaseProfileId == Guid.Empty || value.ProjectId == Guid.Empty ||
                value.LifetimeId == Guid.Empty || value.RequesterId == Guid.Empty ||
                value.ParentProjectId.HasValue != value.ParentLifetimeId.HasValue ||
                value.ParentProjectId == Guid.Empty || value.ParentLifetimeId == Guid.Empty ||
                value.ParentProjectId == value.ProjectId) {
            throw new ArgumentException("Reservation cancellation requires the complete original reservation tuple.", nameof(value));
        }
    }

    private static ProjectDeletionCompletionId Create(string domain, DatasetNamespaceId originalNamespace,
        ProjectWriteAdmission project, string? fingerprint, Guid? reservationId) {
        using var encoded = new MemoryStream(512);
        using (var writer = new Utf8JsonWriter(encoded)) {
            writer.WriteStartObject();
            writer.WriteString("domain", domain);
            writer.WriteString("namespace", originalNamespace.Value);
            writer.WriteString("profile", project.DatabaseProfileId);
            writer.WriteString("project", project.ProjectId);
            writer.WriteString("lifetime", project.LifetimeId);
            if (fingerprint is null) {
                writer.WriteNull("fingerprint");
            } else {
                writer.WriteString("fingerprint", fingerprint);
            }
            if (reservationId is { } reserved) {
                writer.WriteString("reservation", reserved);
            } else {
                writer.WriteNull("reservation");
            }
            writer.WriteEndObject();
        }
        var digest = SHA256.HashData(encoded.GetBuffer().AsSpan(0, checked((int)encoded.Length)));
        Span<byte> uuid = stackalloc byte[16];
        digest.AsSpan(0, uuid.Length).CopyTo(uuid);
        uuid[6] = (byte)((uuid[6] & 0x0f) | 0x80);
        uuid[8] = (byte)((uuid[8] & 0x3f) | 0x80);
        return new(new Guid(uuid, bigEndian: true));
    }
}
