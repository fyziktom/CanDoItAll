using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Tests.Support;
using Microsoft.Playwright;

namespace CanDoItAll.Tests.Playwright;

[Trait("Category", "Playwright")]
[Trait("Category", "HostPlatform")]
public sealed class ProjectPendingSubmissionBrowserTests {
    private const string Origin = "https://projects-pending.test";
    private const string ModulePath = "/js/projectPendingSubmissions.js";
    private const string CallerBinding = "pc1:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string OtherCallerBinding = "pc1:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public async Task Two_tabs_keep_exact_preparation_and_accepted_result_despite_stale_observations() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var first = await fixture.NewPageAsync();
        var second = await fixture.NewPageAsync();
        var key = CreateKey();
        const string frozen = "{\"operation\":{\"epoch\":9223372036854775807},\"name\":\"Original submission\"}";
        var prepared = await PrepareAsync(first, key, frozen);
        Assert.Equal("applied", Status(prepared));
        Assert.Equal("unchanged", Status(await PrepareAsync(second, key, frozen)));
        var mismatch = await Assert.ThrowsAsync<PlaywrightException>(() => PrepareAsync(second, key, "{\"name\":\"changed\"}"));
        Assert.Contains("PreparationMismatch", mismatch.Message);
        string attempt = Guid.NewGuid().ToString("D");
        var dispatched = await BeginAsync(first, key, 1, attempt);
        const string accepted = "{\"revision\":\"historical accepted revision\",\"name\":\"Normalized submission\"}";
        var receipt = await first.EvaluateAsync<JsonElement>("x => pending.observeAccepted(x.key, x.accepted)", new { key, accepted });
        Assert.Equal("applied", Status(receipt));
        Assert.Equal("unchanged", Status(await second.EvaluateAsync<JsonElement>(
            "x => pending.observeAttempt(x.key, x.attempt, 'unconfirmed')", new { key, attempt })));
        Assert.Equal("changed", Status(await second.EvaluateAsync<JsonElement>(
            "x => pending.clearResolved(x.key, x.revision)", new { key, revision = Revision(dispatched) })));
        var retained = await ReadAsync(second, key);
        Assert.Equal("accepted", retained.GetProperty("stage").GetString());
        Assert.Equal(frozen, retained.GetProperty("frozenRequest").GetString());
        Assert.Equal(accepted, retained.GetProperty("acceptedPayload").GetString());
        Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture), retained.GetProperty("key").GetProperty("epoch").GetString());
        var contradiction = await Assert.ThrowsAsync<PlaywrightException>(() => second.EvaluateAsync<JsonElement>(
            "x => pending.observeAccepted(x, '{\"different\":true}')", key));
        Assert.Contains("ContradictoryOutcome", contradiction.Message);
        await first.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.revision)", new { key, revision = Revision(receipt) });
        var recreated = await PrepareAsync(first, key, frozen);
        Assert.True(Revision(recreated) > Revision(receipt));
        await BeginAsync(first, key, Revision(recreated), Guid.NewGuid().ToString("D"));
        await first.EvaluateAsync<JsonElement>("x => pending.observeAccepted(x.key, x.accepted)", new { key, accepted });
        Assert.Equal("changed", Status(await second.EvaluateAsync<JsonElement>(
            "x => pending.clearResolved(x.key, x.revision)", new { key, revision = Revision(receipt) })));
    }

    [Fact]
    public async Task Later_rejection_cannot_resolve_an_earlier_unknown_attempt_but_closed_absence_can() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync();
        var key = CreateKey();
        await PrepareAsync(page, key);
        string firstAttempt = Guid.NewGuid().ToString("D");
        await BeginAsync(page, key, 1, firstAttempt);
        var unknown = await page.EvaluateAsync<JsonElement>("x => pending.observeAttempt(x.key, x.attempt, 'unconfirmed')",
            new { key, attempt = firstAttempt });
        string retryAttempt = Guid.NewGuid().ToString("D");
        await BeginAsync(page, key, Revision(unknown), retryAttempt, explicitRetry: true);
        var rejected = await page.EvaluateAsync<JsonElement>("x => pending.observeAttempt(x.key, x.attempt, 'rejected')",
            new { key, attempt = retryAttempt });
        Assert.Equal("unconfirmed", rejected.GetProperty("record").GetProperty("stage").GetString());
        Assert.True(rejected.GetProperty("record").GetProperty("hadUnconfirmedDispatch").GetBoolean());
        var removal = await Assert.ThrowsAsync<PlaywrightException>(() => page.EvaluateAsync<JsonElement>(
            "x => pending.clearResolved(x.key, x.revision)", new { key, revision = Revision(rejected) }));
        Assert.Contains("RemovalNotAllowed", removal.Message);
        var absent = await page.EvaluateAsync<JsonElement>("x => pending.observeClosedAbsent(x, '{\"closed\":true}')", key);
        Assert.Equal("closedAbsent", absent.GetProperty("record").GetProperty("stage").GetString());
        Assert.Equal("applied", Status(await page.EvaluateAsync<JsonElement>("x => pending.clearResolved(x.key, x.revision)",
            new { key, revision = Revision(absent) })));

        var firstRejectedKey = CreateKey();
        var firstRejectedPrepared = await PrepareAsync(page, firstRejectedKey);
        string rejectedAttempt = Guid.NewGuid().ToString("D");
        await BeginAsync(page, firstRejectedKey, Revision(firstRejectedPrepared), rejectedAttempt);
        var firstRejected = await page.EvaluateAsync<JsonElement>("x => pending.observeAttempt(x.key, x.attempt, 'rejected')",
            new { key = firstRejectedKey, attempt = rejectedAttempt });
        Assert.Equal("rejected", firstRejected.GetProperty("record").GetProperty("stage").GetString());
        Assert.False(firstRejected.GetProperty("record").GetProperty("hadUnconfirmedDispatch").GetBoolean());
    }

    [Fact]
    public async Task Prepared_cancellation_and_dispatch_are_atomic_across_tabs() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var first = await fixture.NewPageAsync();
        var second = await fixture.NewPageAsync();
        var key = CreateKey();
        await PrepareAsync(first, key);
        var outcomes = await Task.WhenAll(
            BeginAsync(first, key, 1, Guid.NewGuid().ToString("D")),
            second.EvaluateAsync<JsonElement>("x => pending.cancelPrepared(x, 1)", key));
        Assert.Single(outcomes, value => Status(value) == "applied");
        Assert.Single(outcomes, value => Status(value) is "changed" or "missing");
        var current = await ReadAsync(first, key);
        if (Status(outcomes[0]) == "applied") {
            Assert.Equal("dispatching", current.GetProperty("stage").GetString());
        } else {
            Assert.Equal(JsonValueKind.Null, current.ValueKind);
        }
    }

    [Fact]
    public async Task Scoped_paging_and_capacity_reject_without_evicting_other_records() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync(recordLimit: 3);
        Guid dataset = Guid.NewGuid();
        var firstKey = CreateKey(dataset);
        var secondKey = CreateKey(dataset);
        var foreignKey = CreateKey(dataset, OtherCallerBinding);
        await PrepareAsync(page, firstKey);
        await PrepareAsync(page, secondKey);
        await PrepareAsync(page, foreignKey);
        var scope = new { @namespace = dataset.ToString("D"), callerBinding = CallerBinding };
        var firstPage = JsonSerializer.Deserialize<JsonElement>(await page.EvaluateAsync<string>(
            "async x => JSON.stringify(await pending.list(x, 1))", scope));
        Assert.Single(firstPage.GetProperty("items").EnumerateArray());
        Assert.False(firstPage.GetProperty("items")[0].TryGetProperty("frozenRequest", out _));
        var next = ReadKey(firstPage.GetProperty("nextKey"));
        Assert.Equal(ReadKey(firstPage.GetProperty("items")[0].GetProperty("key")), next);
        var secondPage = await page.EvaluateAsync<JsonElement>("x => pending.list(x.scope, 1, x.next)", new { scope, next });
        Assert.Single(secondPage.GetProperty("items").EnumerateArray());
        Assert.Equal(JsonValueKind.Null, secondPage.GetProperty("nextKey").ValueKind);
        Assert.NotEqual(firstPage.GetProperty("items")[0].GetProperty("key").GetProperty("id").GetString(),
            secondPage.GetProperty("items")[0].GetProperty("key").GetProperty("id").GetString());
        var capacity = await Assert.ThrowsAsync<PlaywrightException>(() => PrepareAsync(page, CreateKey(dataset)));
        Assert.Contains("CapacityLimit", capacity.Message);
        Assert.Equal("prepared", (await ReadAsync(page, foreignKey)).GetProperty("stage").GetString());
        var paging = await Assert.ThrowsAsync<PlaywrightException>(() => page.EvaluateAsync<JsonElement>("x => pending.list(x, 17)", scope));
        Assert.Contains("PageLimit", paging.Message);
    }

    [Fact]
    public async Task Persistent_browser_reopen_retains_exact_submission_and_historical_acceptance() {
        string profile = TestFileSystem.CreateTemporaryRoot("candoitall-project-pending-browser");
        var key = CreateKey();
        const string frozen = "{\"name\":\"Submitted before browser restart\"}";
        const string accepted = "{\"name\":\"Exact accepted name\"}";
        try {
            using var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            await using (var first = await playwright.Chromium.LaunchPersistentContextAsync(profile, new() {
                Headless = true, ViewportSize = new() { Width = 1920, Height = 1080 }
            })) {
                await ConfigureRoutesAsync(first);
                var page = await NewPageAsync(first);
                await PrepareAsync(page, key, frozen);
                await BeginAsync(page, key, 1, Guid.NewGuid().ToString("D"));
                await page.EvaluateAsync<JsonElement>("x => pending.observeAccepted(x.key, x.accepted)", new { key, accepted });
                await page.EvaluateAsync("() => pending.dispose()");
            }
            await using (var reopened = await playwright.Chromium.LaunchPersistentContextAsync(profile, new() {
                Headless = true, ViewportSize = new() { Width = 1920, Height = 1080 }
            })) {
                await ConfigureRoutesAsync(reopened);
                var page = await NewPageAsync(reopened);
                var restored = await ReadAsync(page, key);
                Assert.Equal("accepted", restored.GetProperty("stage").GetString());
                Assert.Equal(frozen, restored.GetProperty("frozenRequest").GetString());
                Assert.Equal(accepted, restored.GetProperty("acceptedPayload").GetString());
                Assert.Equal(key, ReadKey(restored.GetProperty("key")));
            }
        } finally {
            TestFileSystem.DeleteDirectoryWithRetry(profile);
        }
    }

    [Fact]
    public async Task Abort_after_successful_put_does_not_report_commit_or_consume_capacity() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync(recordLimit: 1);
        var key = CreateKey();
        var result = await page.EvaluateAsync<JsonElement>("""
            async input => {
                const original = IDBObjectStore.prototype.put;
                let effectCalls = 0;
                let errorName = null;
                IDBObjectStore.prototype.put = function (...args) {
                    const request = original.apply(this, args);
                    if (this.name === 'submissions') {
                        request.addEventListener('success', () => this.transaction.abort(), { once: true });
                    }
                    return request;
                };
                try {
                    await pending.prepare(input);
                    effectCalls++;
                } catch (error) {
                    errorName = error.name;
                } finally {
                    IDBObjectStore.prototype.put = original;
                }
                return { effectCalls, errorName, current: await pending.read(input.key) };
            }
            """, Input(key));
        Assert.Equal(0, result.GetProperty("effectCalls").GetInt32());
        Assert.Equal("AbortError", result.GetProperty("errorName").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("current").ValueKind);
        Assert.Equal("applied", Status(await PrepareAsync(page, CreateKey())));
    }

    [Fact]
    public async Task Disposing_store_before_transaction_completion_does_not_authorize_dispatch() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync();
        var key = CreateKey();
        var result = await page.EvaluateAsync<JsonElement>("""
            async input => {
                const original = IDBObjectStore.prototype.put;
                let effectCalls = 0;
                let retired = false;
                IDBObjectStore.prototype.put = function (...args) {
                    const request = original.apply(this, args);
                    if (this.name === 'submissions') {
                        request.addEventListener('success', () => pending.dispose(), { once: true });
                    }
                    return request;
                };
                try {
                    await pending.prepare(input);
                    effectCalls++;
                } catch (error) {
                    retired = error.message.includes('StorageRetired');
                } finally {
                    IDBObjectStore.prototype.put = original;
                }
                return { effectCalls, retired };
            }
            """, Input(key));
        Assert.Equal(0, result.GetProperty("effectCalls").GetInt32());
        Assert.True(result.GetProperty("retired").GetBoolean());
        await page.EvaluateAsync("x => pending.initialize(x)", Limits());
        Assert.Equal("prepared", (await ReadAsync(page, key)).GetProperty("stage").GetString());
    }

    [Fact]
    public async Task Real_browser_quota_failure_blocks_first_dispatch_and_preserves_pending_acceptance_recovery() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync(initialize: false);
        var session = await page.Context.NewCDPSessionAsync(page);
        var key = CreateKey();
        const int quotaBytes = 262_144;
        string oversizedPayload = Convert.ToBase64String(RandomNumberGenerator.GetBytes(786_432));
        try {
            await session.SendAsync("Storage.overrideQuotaForOrigin", new() { ["origin"] = Origin, ["quotaSize"] = quotaBytes });
            var quota = await session.SendAsync("Storage.getUsageAndQuota", new() { ["origin"] = Origin });
            Assert.True(quota.HasValue);
            Assert.True(quota.Value.GetProperty("overrideActive").GetBoolean());
            Assert.Equal((double)quotaBytes, quota.Value.GetProperty("quota").GetDouble());
            await page.EvaluateAsync("x => pending.initialize(x)", Limits());
            var failed = await page.EvaluateAsync<JsonElement>("""
                async input => {
                    let effectCalls = 0;
                    let errorName = null;
                    try {
                        await pending.prepare(input);
                        await pending.beginDispatch(input.key, 1, crypto.randomUUID(), false);
                        effectCalls++;
                    } catch (error) {
                        errorName = error.name;
                    }
                    return { effectCalls, errorName, current: await pending.read(input.key) };
                }
                """, Input(key, oversizedPayload));
            Assert.Equal(0, failed.GetProperty("effectCalls").GetInt32());
            Assert.Equal("QuotaExceededError", failed.GetProperty("errorName").GetString());
            Assert.Equal(JsonValueKind.Null, failed.GetProperty("current").ValueKind);
            var empty = await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)");
            Assert.Equal(0, empty.GetProperty("count").GetInt32());
            Assert.Equal(0, empty.GetProperty("bytes").GetInt64());
            await PrepareAsync(page, key);
            var dispatched = await BeginAsync(page, key, 1, Guid.NewGuid().ToString("D"));
            var beforeAcceptance = await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)");
            var errorName = await page.EvaluateAsync<string?>("""
                async x => {
                    try {
                        await pending.observeAccepted(x.key, x.accepted);
                        return null;
                    } catch (error) {
                        return error.name;
                    }
                }
                """, new { key, accepted = oversizedPayload });
            Assert.Equal("QuotaExceededError", errorName);
            var pending = await ReadAsync(page, key);
            Assert.Equal(key, ReadKey(pending.GetProperty("key")));
            Assert.Equal("dispatching", pending.GetProperty("stage").GetString());
            Assert.Equal(Revision(dispatched), pending.GetProperty("revision").GetInt32());
            Assert.Equal(dispatched.GetProperty("record").GetProperty("frozenRequest").GetString(),
                pending.GetProperty("frozenRequest").GetString());
            Assert.Equal(dispatched.GetProperty("record").GetProperty("attemptId").GetString(),
                pending.GetProperty("attemptId").GetString());
            Assert.Equal(JsonValueKind.Null, pending.GetProperty("acceptedPayload").ValueKind);
            var afterAcceptance = await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)");
            Assert.Equal(1, afterAcceptance.GetProperty("count").GetInt32());
            Assert.Equal(beforeAcceptance.GetProperty("bytes").GetInt64(), afterAcceptance.GetProperty("bytes").GetInt64());
        } finally {
            await session.SendAsync("Storage.overrideQuotaForOrigin", new() { ["origin"] = Origin });
            await session.DetachAsync();
        }
    }

    [Fact]
    public async Task Unsupported_strict_durability_fails_closed_without_memory_or_localstorage_fallback() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync(initialize: false);
        var result = await page.EvaluateAsync<JsonElement>("""
            async options => {
                const original = IDBDatabase.prototype.transaction;
                let errorCode = null;
                IDBDatabase.prototype.transaction = function (stores, mode) {
                    return original.call(this, stores, mode, { durability: 'relaxed' });
                };
                try {
                    await pending.initialize(options);
                } catch (error) {
                    errorCode = error.code;
                } finally {
                    IDBDatabase.prototype.transaction = original;
                }
                return { errorCode, localStorageEntries: localStorage.length };
            }
            """, Limits());
        Assert.Equal("UnsupportedDurability", result.GetProperty("errorCode").GetString());
        Assert.Equal(0, result.GetProperty("localStorageEntries").GetInt32());
        await page.EvaluateAsync("x => pending.initialize(x)", Limits());
        Assert.Equal("applied", Status(await PrepareAsync(page, CreateKey())));
    }

    [Fact]
    public async Task Canonical_operation_keys_reject_final_line_terminators_before_opening_a_storage_transaction() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync(recordLimit: 1);
        var key = CreateKey();
        var result = await page.EvaluateAsync<JsonElement>("""
            async input => {
                const original = IDBDatabase.prototype.transaction;
                const errors = [];
                let transactions = 0;
                IDBDatabase.prototype.transaction = function (...args) {
                    transactions++;
                    return original.apply(this, args);
                };
                try {
                    for (const field of ['namespace', 'id', 'callerBinding', 'epoch']) {
                        for (const suffix of ['\n', '\r\n']) {
                            const key = { ...input.key, [field]: input.key[field] + suffix };
                            let errorCode = null;
                            try {
                                await pending.prepare({ ...input, key });
                            } catch (error) {
                                errorCode = error.code;
                            }
                            errors.push({ field, suffix, errorCode });
                        }
                    }
                } finally {
                    IDBDatabase.prototype.transaction = original;
                }
                return { errors, transactions, inventory: await pending.listReferences(1) };
            }
            """, Input(key));
        var errors = result.GetProperty("errors").EnumerateArray().ToArray();
        Assert.Equal(8, errors.Length);
        Assert.All(errors, error => Assert.Equal("InvalidRequest", error.GetProperty("errorCode").GetString()));
        Assert.Equal(0, result.GetProperty("transactions").GetInt32());
        Assert.Equal(0, result.GetProperty("inventory").GetProperty("count").GetInt32());
        Assert.Empty(result.GetProperty("inventory").GetProperty("items").EnumerateArray());

        var prepared = await PrepareAsync(page, key);
        Assert.Equal("applied", Status(prepared));
        Assert.Equal(1, Revision(prepared));
        var retainedKey = prepared.GetProperty("record").GetProperty("key");
        Assert.Equal(key.@namespace, retainedKey.GetProperty("namespace").GetString());
        Assert.Equal(key.callerBinding, retainedKey.GetProperty("callerBinding").GetString());
        Assert.Equal(key.epoch, retainedKey.GetProperty("epoch").GetString());
        Assert.Equal(key.id, retainedKey.GetProperty("id").GetString());
        Assert.Equal(long.MaxValue.ToString(CultureInfo.InvariantCulture),
            retainedKey.GetProperty("epoch").GetString());
    }

    [Fact]
    public async Task Foreign_reference_inventory_disposal_releases_capacity_without_returning_payloads_or_losing_revision_fencing() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync(recordLimit: 2);
        var first = CreateKey(callerBinding: OtherCallerBinding);
        var second = CreateKey();
        var prepared = await PrepareAsync(page, first, "{\"name\":\"Private frozen body\"}");
        await PrepareAsync(page, second);
        var blocked = await Assert.ThrowsAsync<PlaywrightException>(() => PrepareAsync(page, CreateKey()));
        Assert.Contains("CapacityLimit", blocked.Message);
        var inventory = JsonSerializer.Deserialize<JsonElement>(await page.EvaluateAsync<string>(
            "async () => JSON.stringify(await pending.listReferences(1))"));
        Assert.Equal(2, inventory.GetProperty("count").GetInt32());
        Assert.Single(inventory.GetProperty("items").EnumerateArray());
        var nextKey = ReadKey(inventory.GetProperty("nextKey"));
        Assert.Equal(ReadKey(inventory.GetProperty("items")[0].GetProperty("key")), nextKey);
        var next = await page.EvaluateAsync<JsonElement>("key => pending.listReferences(1, key)", nextKey);
        Assert.Single(next.GetProperty("items").EnumerateArray());
        Assert.DoesNotContain("frozenRequest", inventory.GetRawText());
        Assert.DoesNotContain("Private frozen body", inventory.GetRawText() + next.GetRawText());
        var bytes = inventory.GetProperty("bytes").GetInt64();
        var discarded = await page.EvaluateAsync<JsonElement>("x => pending.discardEvidence(x.key, x.revision)", new { key = first, revision = Revision(prepared) });
        Assert.Equal("applied", Status(discarded));
        Assert.False(discarded.TryGetProperty("record", out _));
        var remaining = await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)");
        Assert.Equal(1, remaining.GetProperty("count").GetInt32());
        Assert.True(remaining.GetProperty("bytes").GetInt64() < bytes);
        var replacement = await PrepareAsync(page, first);
        Assert.True(Revision(replacement) > Revision(prepared));
        var stale = await page.EvaluateAsync<JsonElement>("x => pending.discardEvidence(x.key, x.revision)", new { key = first, revision = Revision(prepared) });
        Assert.Equal("changed", Status(stale));
        Assert.DoesNotContain("frozenRequest", stale.GetRawText());
        Assert.Equal("prepared", (await ReadAsync(page, first)).GetProperty("stage").GetString());
    }

    [Fact]
    public async Task Accepted_and_explicit_discard_race_by_exact_revision_without_disclosing_the_accepted_body() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var first = await fixture.NewPageAsync();
        var second = await fixture.NewPageAsync();
        var key = CreateKey();
        var prepared = await PrepareAsync(first, key);
        var dispatched = await BeginAsync(first, key, Revision(prepared), Guid.NewGuid().ToString("D"));
        var outcomes = await Task.WhenAll(
            first.EvaluateAsync<JsonElement>("key => pending.observeAccepted(key, '{\"private\":\"Accepted private body\"}')", key),
            second.EvaluateAsync<JsonElement>("x => pending.discardEvidence(x.key, x.revision)", new { key, revision = Revision(dispatched) }));
        Assert.Single(outcomes, result => Status(result) == "applied");
        Assert.Contains(Status(outcomes[1]), new[] { "applied", "changed" });
        Assert.DoesNotContain("Accepted private body", outcomes[1].GetRawText());
        Assert.DoesNotContain("acceptedPayload", outcomes[1].GetRawText());
        Assert.DoesNotContain("frozenRequest", outcomes[1].GetRawText());
        if (Status(outcomes[1]) == "changed") {
            Assert.Equal("accepted", outcomes[1].GetProperty("reference").GetProperty("stage").GetString());
            Assert.Equal("accepted", (await ReadAsync(second, key)).GetProperty("stage").GetString());
        } else {
            Assert.Equal("missing", Status(outcomes[0]));
            Assert.Equal(JsonValueKind.Null, (await ReadAsync(second, key)).ValueKind);
        }
    }

    [Fact]
    public async Task Aborted_discard_keeps_accounting_and_unsupported_payload_can_be_discarded_without_decoding() {
        await using var fixture = await BrowserStoreFixture.StartAsync();
        var page = await fixture.NewPageAsync();
        var key = CreateKey();
        var prepared = await PrepareAsync(page, key);
        var before = await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)");
        Assert.True(await page.EvaluateAsync<bool>("""
            async x => {
                const original = IDBObjectStore.prototype.delete;
                IDBObjectStore.prototype.delete = function (...args) {
                    const request = original.apply(this, args);
                    this.transaction.abort();
                    return request;
                };
                try {
                    await pending.discardEvidence(x.key, x.revision);
                    return false;
                } catch { return true; }
                finally { IDBObjectStore.prototype.delete = original; }
            }
            """, new { key, revision = Revision(prepared) }));
        Assert.Equal(before.GetRawText(), (await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)")).GetRawText());
        await page.EvaluateAsync("""
            async key => {
                const db = await new Promise((resolve, reject) => {
                    const request = indexedDB.open('candoitall.projects.pending-submissions', 1);
                    request.onsuccess = () => resolve(request.result);
                    request.onerror = () => reject(request.error);
                });
                try {
                    await new Promise((resolve, reject) => {
                        const tx = db.transaction('submissions', 'readwrite', { durability: 'strict' });
                        tx.oncomplete = resolve;
                        tx.onabort = () => reject(tx.error);
                        const store = tx.objectStore('submissions');
                        const request = store.get([key.namespace, key.callerBinding, key.epoch, key.id]);
                        request.onsuccess = () => {
                            const record = request.result;
                            record.frozenRequest = 'x'.repeat(record.frozenRequest.length);
                            store.put(record);
                        };
                    });
                } finally { db.close(); }
            }
            """, key);
        var metadata = await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)");
        Assert.Equal(before.GetRawText(), metadata.GetRawText());
        var removed = await page.EvaluateAsync<JsonElement>("x => pending.discardEvidence(x.key, x.revision)", new { key, revision = Revision(prepared) });
        Assert.Equal("applied", Status(removed));
        var empty = await page.EvaluateAsync<JsonElement>("() => pending.listReferences(16)");
        Assert.Equal(0, empty.GetProperty("count").GetInt32());
        Assert.Equal(0, empty.GetProperty("bytes").GetInt64());
    }

    private sealed record PendingStoreKey(string @namespace, string callerBinding, string epoch, string id);

    private static PendingStoreKey CreateKey(Guid? dataset = null, string callerBinding = CallerBinding) => new(
        (dataset ?? Guid.NewGuid()).ToString("D"), callerBinding,
        long.MaxValue.ToString(CultureInfo.InvariantCulture), Guid.NewGuid().ToString("D"));

    private static PendingStoreKey ReadKey(JsonElement key) => new(
        Assert.IsType<string>(key.GetProperty("namespace").GetString()),
        Assert.IsType<string>(key.GetProperty("callerBinding").GetString()),
        Assert.IsType<string>(key.GetProperty("epoch").GetString()),
        Assert.IsType<string>(key.GetProperty("id").GetString()));

    private static object Input(PendingStoreKey key, string frozenRequest = "{\"name\":\"Frozen project\"}")
        => new { key, frozenRequest, targetProjectId = (string?)null, hadStarterPlan = false };

    private static object Limits(int recordLimit = 16) => new {
        maximumRecordCount = recordLimit, maximumTotalBytes = 16_777_216,
        maximumFrozenRequestBytes = 2_097_152, maximumAcceptedBytes = 2_097_152, maximumPageSize = 16
    };

    private static string? Status(JsonElement outcome) => outcome.GetProperty("status").GetString();
    private static int Revision(JsonElement outcome) => outcome.GetProperty("record").GetProperty("revision").GetInt32();

    private static Task<JsonElement> PrepareAsync(IPage page, PendingStoreKey key, string frozen = "{\"name\":\"Frozen project\"}")
        => page.EvaluateAsync<JsonElement>("x => pending.prepare(x)", Input(key, frozen));

    private static Task<JsonElement> BeginAsync(IPage page, PendingStoreKey key, int revision, string attempt, bool explicitRetry = false)
        => page.EvaluateAsync<JsonElement>("x => pending.beginDispatch(x.key, x.revision, x.attempt, x.explicitRetry)",
            new { key, revision, attempt, explicitRetry });

    private static Task<JsonElement> ReadAsync(IPage page, PendingStoreKey key)
        => page.EvaluateAsync<JsonElement>("x => pending.read(x)", key);

    private static async Task ConfigureRoutesAsync(IBrowserContext context) {
        string module = await File.ReadAllTextAsync(Path.Combine(PlaywrightTestHostPaths.RepositoryRoot,
            "src", "App", "CanDoItAll.Web", "wwwroot", "js", "projectPendingSubmissions.js"));
        await context.RouteAsync(Origin + "/**", route => route.FulfillAsync(new() {
            ContentType = route.Request.Url.EndsWith(ModulePath, StringComparison.Ordinal) ? "text/javascript" : "text/html",
            Body = route.Request.Url.EndsWith(ModulePath, StringComparison.Ordinal) ? module : "<!doctype html><title>Pending submission storage proof</title>"
        }));
    }

    private static async Task<IPage> NewPageAsync(IBrowserContext context, int recordLimit = 16, bool initialize = true) {
        var page = await context.NewPageAsync();
        page.SetDefaultTimeout(15_000);
        await page.GotoAsync(Origin);
        await page.EvaluateAsync("async path => { window.pending = await import(path); }", ModulePath);
        if (initialize) {
            await page.EvaluateAsync("x => pending.initialize(x)", Limits(recordLimit));
        }
        return page;
    }

    private sealed class BrowserStoreFixture(IPlaywright playwright, IBrowser browser, IBrowserContext context) : IAsyncDisposable {
        public static async Task<BrowserStoreFixture> StartAsync() {
            var playwright = await Microsoft.Playwright.Playwright.CreateAsync();
            IBrowser? browser = null;
            IBrowserContext? context = null;
            try {
                browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
                context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1920, Height = 1080 } });
                await ConfigureRoutesAsync(context);
                return new(playwright, browser, context);
            } catch (Exception failure) {
                try {
                    await TestFixtureCleanup.DisposeAfterFailureAsync(failure, context, browser);
                } finally {
                    playwright.Dispose();
                }
                throw;
            }
        }

        public Task<IPage> NewPageAsync(int recordLimit = 16, bool initialize = true)
            => ProjectPendingSubmissionBrowserTests.NewPageAsync(context, recordLimit, initialize);

        public async ValueTask DisposeAsync() {
            try {
                await TestFixtureCleanup.DisposeAsync(context, browser);
            } finally {
                playwright.Dispose();
            }
        }
    }
}
