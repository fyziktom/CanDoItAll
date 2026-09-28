using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Web.Api;

internal static class StoragePlacementRecoveryEndpoints {
    internal const string Route = "/api/storage-placement-recovery";

    internal static IEndpointRouteBuilder MapStoragePlacementRecoveryApi(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(Route).WithTags("Storage recovery");
        group.ApplyApiAuthorization(endpoints, ApiAuthorizationPolicies.ReadStoragePlacementRecovery);
        group.MapGet("/context", GetContextAsync)
            .Produces<StoragePlacementRecoveryContext>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status403Forbidden, StatusCodes.Status409Conflict,
                StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/pending", ListPendingAsync)
            .Produces<StoragePlacementRecoveryPage>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/pending-continuations", ListPendingContinuationsAsync)
            .Produces<StoragePlacementContinuationPage>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/{intentId:guid}", GetIntentAsync)
            .Produces<StoragePlacementRecoveryItem>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/reconcile", ReconcileAsync)
            .Produces<StoragePlacementRecoveryItem>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/verify-external-termination", VerifyExternalTerminationAsync)
            .Produces<StoragePlacementRecoveryItem>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        group.MapGet("/{intentId:guid}/owner-continuation", GetOwnerContinuationAsync)
            .Produces<StoragePlacementOwnerContinuationObservation>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/reconcile-cancelled-run-receipts", ReconcileCancelledRunReceiptsAsync)
            .Produces<StoragePlacementOwnerContinuationObservation>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        group.MapPost("/continue-workflow-asset", ContinueWorkflowAssetAsync)
            .Produces<StoragePlacementOwnerContinuationObservation>()
            .ProducesStoragePlacementRecoveryFailures(StatusCodes.Status400BadRequest, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound, StatusCodes.Status409Conflict, StatusCodes.Status503ServiceUnavailable);
        return endpoints;
    }

    /// <summary>
    /// Read the database runtime context that every other storage placement recovery request must carry.
    /// </summary>
    /// <remarks>
    /// Storage placement recovery resolves stable storage placements whose outcome was left open: a planned write of
    /// an asset to a storage provider that was not confirmed, or a completed write whose owner (the process step or
    /// workflow that produced the asset) did not record it. Every recovery request carries this context, which names
    /// the host's active database profile and its generation. Call this operation first, and again after any 409
    /// <c>StaleContext</c>: the context changes when the host switches its database profile, and requests with an old
    /// context are rejected. The read has no side effects.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c> and
    /// <c>api.storage-placement-recovery.read</c>. The check is repeated during the request, so a token revoked or
    /// narrowed meanwhile gets 403.
    /// </remarks>
    /// <response code="200">The current database runtime context.</response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="409">
    /// <c>StaleContext</c>: the host switched its database profile during the read; call this operation again.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the context could not be read. Nothing was changed; try again later.
    /// </response>
    internal static Task<IResult> GetContextAsync(StoragePlacementRecoveryService recovery, HttpContext http,
        ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => recovery.GetCurrentContextAsync(http.RequestAborted), logger);

    /// <summary>
    /// List stable storage placements whose write is unresolved, oldest first, one page at a time.
    /// </summary>
    /// <remarks>
    /// Returns placements in the storage states <c>Prepared</c>, <c>Dispatching</c>, <c>Uncertain</c> and
    /// <c>Conflict</c>, ordered by creation time and then by intent identifier. Completed and deleted placements are
    /// not listed; for completed placements whose owner has not recorded them, use
    /// <c>GET /api/storage-placement-recovery/pending-continuations</c>. Each item says which action, if any, the
    /// caller may take (<c>availableAction</c>) or why none is possible (<c>block</c>). The items contain no content,
    /// storage addresses or paths. The read has no side effects.
    ///
    /// Paging is offset-based: send <c>offset</c> = <c>nextOffset</c> of the previous page until <c>nextOffset</c> is
    /// null. After a 409 <c>StaleContext</c>, read the context again and restart from offset 0.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member. A missing or malformed
    /// <c>databaseProfileId</c> or <c>generation</c> is rejected by the framework with 400 before the operation runs,
    /// without that object.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c> and
    /// <c>api.storage-placement-recovery.read</c>. Without the write scopes, a placement that could otherwise be
    /// reconciled shows <c>block</c> <c>ReadOnlyAuthority</c> instead of an action.
    /// </remarks>
    /// <param name="databaseProfileId">
    /// <c>databaseProfileId</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <param name="generation">
    /// <c>generation</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <param name="projectId">
    /// Optional project filter: only placements planned for this project. Must not be the empty GUID.
    /// </param>
    /// <param name="storageId">
    /// Optional storage filter: only placements planned for this storage catalog entry. Must not be the empty GUID.
    /// </param>
    /// <param name="take">Page size, from 1 through 128. Omitted means 32.</param>
    /// <param name="offset">
    /// Number of placements to skip, from 0 through 2,147,483,518. Omitted means 0; use <c>nextOffset</c> of the
    /// previous page.
    /// </param>
    /// <response code="200">
    /// One page of unresolved placements; an empty <c>items</c> array means none matched.
    /// </response>
    /// <response code="400">
    /// <c>InvalidRequest</c>: <c>take</c> or <c>offset</c> is out of range, a filter is the empty GUID, or the context
    /// is invalid.
    /// </response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="409">
    /// <c>StaleContext</c>: the context is not the host's current one; read it again and restart from offset 0.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the placements could not be read. Nothing was changed; try again later.
    /// </response>
    internal static Task<IResult> ListPendingAsync(Guid databaseProfileId, long generation, Guid? projectId,
        Guid? storageId, int? take, int? offset, StoragePlacementRecoveryService recovery, HttpContext http,
        ILogger<StoragePlacementRecoveryService> logger) => ResultAsync(() => recovery.ListPendingAsync(
            new(new(databaseProfileId, generation), projectId, storageId, take ?? 32, offset ?? 0),
            http.RequestAborted), logger);

    /// <summary>
    /// List completed storage placements whose owner has not finished recording them, one page at a time.
    /// </summary>
    /// <remarks>
    /// The storage write of these placements completed (storage state <c>Completed</c>, or <c>Deleted</c> after a later
    /// deletion), but the owner that produced the asset (a process step's agent run or a workflow run) has not finished
    /// recording it; <c>phase</c> says which step is missing. Use
    /// <c>GET /api/storage-placement-recovery/{intentId}/owner-continuation</c> on an item to see whether a
    /// continuation can finish it. The items contain no content, storage addresses or paths. The read has no side
    /// effects.
    ///
    /// Paging is offset-based over the owners' candidate records: <c>take</c> bounds the candidates examined, so a
    /// page can hold fewer items than <c>take</c>, or none, while <c>nextOffset</c> is not null. Continue with
    /// <c>offset</c> = <c>nextOffset</c> until it is null. After a 409 <c>StaleContext</c>, read the context again and
    /// restart from offset 0.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member. A missing or malformed
    /// <c>databaseProfileId</c> or <c>generation</c> is rejected by the framework with 400 before the operation runs,
    /// without that object.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c> and
    /// <c>api.storage-placement-recovery.read</c>.
    /// </remarks>
    /// <param name="databaseProfileId">
    /// <c>databaseProfileId</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <param name="generation">
    /// <c>generation</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <param name="projectId">
    /// Optional project filter: only placements of this project. Must not be the empty GUID.
    /// </param>
    /// <param name="storageId">
    /// Optional storage filter: only placements in this storage catalog entry. Must not be the empty GUID.
    /// </param>
    /// <param name="take">Number of owner candidates to examine, from 1 through 128. Omitted means 32.</param>
    /// <param name="offset">
    /// Number of owner candidates to skip, from 0 through 2,147,483,518. Omitted means 0; use <c>nextOffset</c> of the
    /// previous page.
    /// </param>
    /// <response code="200">
    /// One page of placements waiting for their owner; an empty <c>items</c> array ends the list only when
    /// <c>nextOffset</c> is null.
    /// </response>
    /// <response code="400">
    /// <c>InvalidRequest</c>: <c>take</c> or <c>offset</c> is out of range, a filter is the empty GUID, or the context
    /// is invalid.
    /// </response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="409">
    /// <c>StaleContext</c>: the context is not the host's current one; read it again and restart from offset 0.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the placements or the owners' records could not be read consistently. Nothing was changed;
    /// try again later.
    /// </response>
    internal static Task<IResult> ListPendingContinuationsAsync(Guid databaseProfileId, long generation,
        Guid? projectId, Guid? storageId, int? take, int? offset, StoragePlacementRecoveryService recovery,
        HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => recovery.ListPendingContinuationsAsync(
            new(new(databaseProfileId, generation), projectId, storageId, take ?? 32, offset ?? 0),
            http.RequestAborted), logger);

    /// <summary>
    /// Read the recovery state of one stable storage placement by its intent identifier.
    /// </summary>
    /// <remarks>
    /// Returns the placement's storage state, whether its storage and owner receipts exist, and which recovery action
    /// the caller may take (<c>availableAction</c>) or why none is possible (<c>block</c>). Read it before any recovery
    /// command and again after one, because a command's response can be followed by further changes. The item contains
    /// no content, storage addresses or paths. The read has no side effects.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member. A missing or malformed
    /// <c>databaseProfileId</c> or <c>generation</c> is rejected by the framework with 400 before the operation runs,
    /// without that object.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c> and
    /// <c>api.storage-placement-recovery.read</c>. Without the write scopes, a placement that could otherwise be
    /// reconciled shows <c>block</c> <c>ReadOnlyAuthority</c> instead of an action.
    /// </remarks>
    /// <param name="intentId">
    /// Identifier (GUID) of the placement intent, as <c>identity.intentId.value</c> in the list operations. Must not be
    /// the empty GUID.
    /// </param>
    /// <param name="databaseProfileId">
    /// <c>databaseProfileId</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <param name="generation">
    /// <c>generation</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <response code="200">The placement's current recovery state.</response>
    /// <response code="400"><c>InvalidRequest</c>: the context is invalid.</response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="404"><c>NotFound</c>: no placement intent has this identifier in the active database.</response>
    /// <response code="409">
    /// <c>StaleContext</c>: the context is not the host's current one; read it again.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the placement could not be read, including when <c>intentId</c> is the empty GUID. Nothing
    /// was changed.
    /// </response>
    internal static Task<IResult> GetIntentAsync(Guid intentId, Guid databaseProfileId, long generation,
        StoragePlacementRecoveryService recovery, HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => recovery.GetAsync(new(new(databaseProfileId, generation), new(intentId)),
            http.RequestAborted), logger);

    /// <summary>
    /// Reconcile an unresolved stable storage placement by reading back its exact original target.
    /// </summary>
    /// <remarks>
    /// Use it when a read shows <c>availableAction</c> <c>Reconcile</c>. The host re-reads the exact target that was
    /// prepared for the placement and compares its length and SHA-256 hash with the planned content. It never uploads
    /// the content again, never chooses another target and never deletes what it finds. The original project must
    /// still exist in the lifetime it had when the placement was planned.
    ///
    /// HTTP 200 means that reconciliation ran, not that the placement is complete: the body is the item read after
    /// the attempt, and <c>storageState</c> tells the result.
    ///
    /// - <c>Completed</c>: the content was verified at the target and the storage receipt recorded.
    /// - <c>Uncertain</c>: the outcome still could not be verified; the target is kept. Do not treat it as stored and
    /// do not write the content elsewhere; reconcile again later or investigate the storage.
    /// - <c>Conflict</c>: the target holds different content, which was preserved and not overwritten.
    ///
    /// A write to FTP without a completion acknowledgment cannot be reconciled until an operator attests that its
    /// server-side dispatch stopped; the item then shows <c>availableAction</c> <c>VerifyExternalTermination</c>.
    ///
    /// Repeating the request is another read-back of the same target and never writes content, but a failure response
    /// does not prove that nothing changed: after 403, 409 or 503 read the item again before deciding the next step.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c>,
    /// <c>api.storage-placement-recovery.read</c>, <c>api.project-structure.write</c> and
    /// <c>api.storage-placement-recovery.reconcile</c>.
    /// </remarks>
    /// <param name="request">The context and the intent of the placement to reconcile.</param>
    /// <response code="200">
    /// Reconciliation ran; <c>storageState</c> of the returned item tells whether the placement completed.
    /// </response>
    /// <response code="400"><c>InvalidRequest</c>: the context is invalid.</response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="404"><c>NotFound</c>: no placement intent has this identifier in the active database.</response>
    /// <response code="409">
    /// <c>Blocked</c>: reconciliation is not available for the placement now (read the item for <c>block</c> and
    /// <c>availableAction</c>), or <c>StaleContext</c>: the context is not the host's current one.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the request could not complete, and it may have stopped after changing the placement. Read
    /// the item again before any further step; do not repeat blindly.
    /// </response>
    internal static Task<IResult> ReconcileAsync(StoragePlacementRecoveryCommand request,
        StoragePlacementRecoveryService recovery, HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => recovery.ReconcileAsync(request, http.RequestAborted), logger);

    /// <summary>
    /// Record an operator's attestation that an unacknowledged FTP upload has stopped, then reconcile the placement.
    /// </summary>
    /// <remarks>
    /// Use it only when a read shows <c>availableAction</c> <c>VerifyExternalTermination</c>: an FTP upload was
    /// dispatched but its completion was never acknowledged, so the host cannot tell whether the server is still
    /// receiving data. Send the request only after verifying outside the host, for example on the FTP server, that no
    /// further data can arrive for that upload. The attestation is recorded durably and cannot be withdrawn through
    /// this API. The host then reconciles the placement as <c>POST /api/storage-placement-recovery/reconcile</c> does,
    /// reading back the exact target without uploading again.
    ///
    /// HTTP 200 means that the attestation was recorded and reconciliation ran; <c>storageState</c> of the returned
    /// item tells the result (<c>Completed</c>, <c>Uncertain</c> or <c>Conflict</c>). A failure response does not prove
    /// that nothing changed: after 403, 409 or 503 read the item again before deciding the next step.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c>,
    /// <c>api.storage-placement-recovery.read</c>, <c>api.project-structure.write</c>,
    /// <c>api.storage-placement-recovery.reconcile</c> and
    /// <c>api.storage-placement-recovery.verify-external-termination</c>. Grant the last scope only to operators who
    /// can verify the FTP server.
    /// </remarks>
    /// <param name="request">The context, the intent and the explicit attestation.</param>
    /// <response code="200">
    /// The attestation was recorded and reconciliation ran; <c>storageState</c> of the returned item tells the result.
    /// </response>
    /// <response code="400">
    /// <c>InvalidRequest</c>: <c>verifiedExternalDispatchStopped</c> is not true, or the context is invalid. Nothing
    /// was recorded.
    /// </response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="404"><c>NotFound</c>: no placement intent has this identifier in the active database.</response>
    /// <response code="409">
    /// <c>Blocked</c>: the placement does not need or allow the attestation now (read the item for <c>block</c> and
    /// <c>availableAction</c>), or <c>StaleContext</c>: the context is not the host's current one.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the request could not complete, and it may have stopped after recording the attestation or
    /// changing the placement. Read the item again before any further step; do not repeat blindly.
    /// </response>
    internal static Task<IResult> VerifyExternalTerminationAsync(
        StoragePlacementExternalTerminationVerification request, StoragePlacementRecoveryService recovery,
        HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => recovery.RecordOperatorVerifiedExternalDispatchTerminationAsync(request,
            http.RequestAborted), logger);

    /// <summary>
    /// Read whether the owner of a completed storage placement recorded it, and which continuation can finish it.
    /// </summary>
    /// <remarks>
    /// Applies to placements listed by <c>GET /api/storage-placement-recovery/pending-continuations</c>: the storage
    /// write completed, but the owner that produced the asset may not have recorded it. The observation returns the
    /// placement (<c>storage</c>), the owner's state (<c>state</c>) and the continuation the caller may run
    /// (<c>availableAction</c>):
    ///
    /// - <c>ReconcileCancelledRunReceipts</c>: run
    /// <c>POST /api/storage-placement-recovery/reconcile-cancelled-run-receipts</c>.
    /// - <c>RecordWorkflowAssetReceipt</c> or <c>CompletePreparedWorkflowAsset</c>: run
    /// <c>POST /api/storage-placement-recovery/continue-workflow-asset</c> with that action and the returned
    /// <c>workflowIntent</c>.
    /// - <c>None</c>: nothing can be run now; <c>state</c> says why.
    ///
    /// The read has no side effects.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member. A missing or malformed
    /// <c>databaseProfileId</c> or <c>generation</c> is rejected by the framework with 400 before the operation runs,
    /// without that object.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c> and
    /// <c>api.storage-placement-recovery.read</c>; without the write scopes <c>state</c> is <c>ReadOnlyAuthority</c>
    /// where an action would otherwise be offered.
    /// </remarks>
    /// <param name="intentId">
    /// Identifier (GUID) of the placement intent, as <c>storage.identity.intentId.value</c> in
    /// <c>GET /api/storage-placement-recovery/pending-continuations</c>. Must not be the empty GUID.
    /// </param>
    /// <param name="databaseProfileId">
    /// <c>databaseProfileId</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <param name="generation">
    /// <c>generation</c> of the context returned by <c>GET /api/storage-placement-recovery/context</c>.
    /// </param>
    /// <response code="200">The owner's continuation state for the placement.</response>
    /// <response code="400"><c>InvalidRequest</c>: the context is invalid.</response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="404"><c>NotFound</c>: no placement intent has this identifier in the active database.</response>
    /// <response code="409">
    /// <c>StaleContext</c>: the context is not the host's current one; read it again.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the owner's state could not be read, including when <c>intentId</c> is the empty GUID.
    /// Nothing was changed.
    /// </response>
    internal static Task<IResult> GetOwnerContinuationAsync(Guid intentId, Guid databaseProfileId, long generation,
        IStoragePlacementOwnerContinuation continuation, HttpContext http,
        ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => continuation.GetAsync(new(new(databaseProfileId, generation), new(intentId)),
            http.RequestAborted), logger);

    /// <summary>
    /// Reconcile the tool receipts of a cancelled agent run so that it records the process asset it placed in storage.
    /// </summary>
    /// <remarks>
    /// Use it when <c>GET /api/storage-placement-recovery/{intentId}/owner-continuation</c> shows
    /// <c>availableAction</c> <c>ReconcileCancelledRunReceipts</c>: a process step's agent run was cancelled after its
    /// asset was placed in storage, and the run did not record the asset. The host reconciles the cancelled run: it
    /// records the tool effect only when the owner confirms it with a receipt, and it never runs the tool again or
    /// writes the content again.
    ///
    /// The body is the observation read after the attempt; read <c>state</c>. <c>ReceiptRecorded</c> means the asset is
    /// now recorded. <c>NativeReceiptNotObserved</c> means no receipt was found yet; the action stays available and the
    /// effect remains uncertain, because an earlier transaction of the owner could still commit. Other states, such as
    /// <c>OwnerReceiptUnconfirmed</c>, <c>CurrentReadDenied</c> or <c>ContinuationUnavailable</c>, mean the receipt
    /// could not be reconciled this way. When the owner state is neither <c>Ready</c> nor <c>ReceiptRecorded</c>, the
    /// request changes nothing and returns the current observation, still with HTTP 200.
    ///
    /// A failure response does not prove that nothing changed: after 403, 409 or 503 read the owner continuation again
    /// before deciding the next step.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c>,
    /// <c>api.storage-placement-recovery.read</c>, <c>api.project-structure.write</c> and
    /// <c>api.storage-placement-recovery.reconcile</c>.
    /// </remarks>
    /// <param name="request">The context and the intent of the completed placement.</param>
    /// <response code="200">The observation after the attempt; <c>state</c> tells the result.</response>
    /// <response code="400"><c>InvalidRequest</c>: the context is invalid.</response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, or the token is missing, invalid, revoked or lacks a required scope.
    /// </response>
    /// <response code="404"><c>NotFound</c>: no placement intent has this identifier in the active database.</response>
    /// <response code="409">
    /// <c>StaleContext</c>: the context is not the host's current one, or the host switched its database profile during
    /// the request.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the request could not complete, and it may have stopped after recording receipts. Read the
    /// owner continuation again before any further step; do not repeat blindly.
    /// </response>
    internal static Task<IResult> ReconcileCancelledRunReceiptsAsync(StoragePlacementRecoveryCommand request,
        IStoragePlacementOwnerContinuation continuation, HttpContext http,
        ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => continuation.ReconcileCancelledRunReceiptsAsync(request, http.RequestAborted), logger);

    /// <summary>
    /// Finish a workflow asset whose storage placement completed, using the continuation the owner offers.
    /// </summary>
    /// <remarks>
    /// Use it when <c>GET /api/storage-placement-recovery/{intentId}/owner-continuation</c> shows
    /// <c>availableAction</c> <c>CompletePreparedWorkflowAsset</c> (create the workflow's prepared asset node in the
    /// project structure from the completed placement) or <c>RecordWorkflowAssetReceipt</c> (the node exists; record
    /// the workflow's receipt). Send that action and the <c>workflowIntent</c> of the same read unchanged as
    /// <c>expectedIntent</c>. The host checks both against the current state; when anything changed after your read,
    /// the request is rejected with 409 <c>Blocked</c> and nothing is continued. The content is never written again.
    ///
    /// The body is the observation read after the attempt; read <c>state</c> and <c>availableAction</c>.
    /// <c>ReceiptRecorded</c> means the asset is recorded. <c>WorkflowManifestPending</c> means the asset is in the
    /// project structure but the workflow has not yet acknowledged it; after a new read, run the operation again with
    /// <c>RecordWorkflowAssetReceipt</c>.
    ///
    /// A failure response does not prove that nothing changed: after 403, 409 or 503 read the owner continuation again
    /// before deciding the next step.
    ///
    /// Failures return a JSON object with a single <c>failure</c> member.
    ///
    /// Authority: usable only while API authorization is enabled; otherwise every request is refused with 403
    /// (<c>Denied</c>). The bearer token must hold the exact scopes <c>api</c>,
    /// <c>api.storage-placement-recovery.read</c>, <c>api.project-structure.write</c> and
    /// <c>api.storage-placement-recovery.reconcile</c>.
    /// </remarks>
    /// <param name="request">
    /// The context, the intent, the action to run and the workflow intent from the latest owner-continuation read.
    /// </param>
    /// <response code="200">The observation after the attempt; <c>state</c> tells the result.</response>
    /// <response code="400">
    /// <c>InvalidRequest</c>: the action is not <c>RecordWorkflowAssetReceipt</c> or
    /// <c>CompletePreparedWorkflowAsset</c>, <c>expectedIntent</c> is missing or malformed, or the context is invalid.
    /// Nothing was changed.
    /// </response>
    /// <response code="403">
    /// <c>Denied</c>: authorization is disabled, the token is missing, invalid, revoked or lacks a required scope, or
    /// the project structure refused the change.
    /// </response>
    /// <response code="404"><c>NotFound</c>: no placement intent has this identifier in the active database.</response>
    /// <response code="409">
    /// <c>Blocked</c>: the action or workflow intent no longer matches the current state, the output conflicts with the
    /// project, or the project write was not admitted; or <c>StaleContext</c>: the context is not the host's current
    /// one. Read the owner continuation again.
    /// </response>
    /// <response code="503">
    /// <c>Unavailable</c>: the request could not complete, and it may have stopped after changing the project. Read the
    /// owner continuation again before any further step; do not repeat blindly.
    /// </response>
    internal static Task<IResult> ContinueWorkflowAssetAsync(StoragePlacementWorkflowContinuationCommand request,
        IStoragePlacementOwnerContinuation continuation, HttpContext http,
        ILogger<StoragePlacementRecoveryService> logger)
        => ResultAsync(() => continuation.ReconcileWorkflowAssetAsync(request, http.RequestAborted), logger);

    private static async Task<IResult> ResultAsync<T>(Func<Task<T>> operation, ILogger logger) {
        try {
            return Results.Ok(await operation());
        } catch (StoragePlacementRecoveryException exception) {
            var status = exception.Failure switch {
                StoragePlacementRecoveryFailure.Denied => StatusCodes.Status403Forbidden,
                StoragePlacementRecoveryFailure.NotFound => StatusCodes.Status404NotFound,
                StoragePlacementRecoveryFailure.InvalidRequest => StatusCodes.Status400BadRequest,
                StoragePlacementRecoveryFailure.StaleContext or StoragePlacementRecoveryFailure.Blocked => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status503ServiceUnavailable
            };
            return Results.Json(new { exception.Failure }, statusCode: status);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.LogWarning("Storage recovery could not complete ({FailureType}). Refresh the original intent status; no provider write retry was requested.",
                exception.GetType().Name);
            return Results.Json(new { Failure = StoragePlacementRecoveryFailure.Unavailable },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
