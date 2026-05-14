(function() {
  if (window.ZyCanvasCalendar) {
    return;
  }
  var shared = window.ZyCanvasCalendarModule;
  if (!shared) { throw new Error('ZyCanvasCalendar foundation must load before 03-editor-and-toolbar.js.'); }
  var { CanvasSurface, HitRegistry, DateMath, drawMiniMonth, drawTimedGrid, fillRoundedPanel, fitText, wrapText, STYLE_ID, DAY_SHORT, MONTH_SHORT, TIMEZONE_FALLBACKS, injectStyles, asText, asNumber, clamp, safeObject, safeArray, copy, escapeHtml, padNumber, ensureDateKey, normalizeIsoString, minutesToClockLabel, formatterKey, getFormatter, getZonedParts, zonedPartsToDateKey, getDateKeyFromIso, getMinutesFromIso, formatDateKeyLabel, formatDateTimeLabel, formatRangeLabel, formatRangeLabelLines, renderListRangeLabel, renderCalendarActionIcon, renderCalendarToolbarIcon, renderCalendarListActionButton, renderCalendarToolbarIconButton, renderCalendarExportMenuItem, renderCalendarAddEventButton, toLocalInputValue, parseLocalInputValue, zonedLocalToUtcIso, localInputToUtcIso, buildUtcIsoFromDateKeyMinutes, addMinutesToIso, addDaysToIso, durationMinutes, createLocalEventId, normalizeEvent, pluralize, formatConnectionLabel, compareEvents, getEventSpan, compareDateKeys, eventSpansDate, eventIntersectsRange, buildDensityMap, buildTimeZoneList, buildDefaultEvent, formatPeriodLabel, scopeRange, CalendarController } = shared;
  CalendarController.prototype.isCurrentEditorEventSaved = function() {
    return !!this.editorEvent && this.editorMode !== 'create' && asText(this.editorEvent.id || this.editorEvent.eventId) !== '';
  };

  CalendarController.prototype.isPlaylistLinkedToEditorEvent = function(playlistId) {
    var safePlaylistId = asText(playlistId);
    if (safePlaylistId === '' || !this.editorEvent) {
      return false;
    }

    return safeArray(this.editorEvent.linkedPlaylists).some(function(playlist) {
      return asText(safeObject(playlist).playlistId) === safePlaylistId;
    });
  };

  CalendarController.prototype.findLinkedPlaylistById = function(playlistId) {
    var safePlaylistId = asText(playlistId);
    return safeArray(this.editorEvent && this.editorEvent.linkedPlaylists).find(function(playlist) {
      return asText(safeObject(playlist).playlistId) === safePlaylistId;
    }) || null;
  };

  CalendarController.prototype.findPlaylistSearchResultById = function(playlistId) {
    var safePlaylistId = asText(playlistId);
    return safeArray(this.editorPlaylistResultsData).find(function(playlist) {
      return asText(safeObject(playlist).playlistId) === safePlaylistId;
    }) || null;
  };

  CalendarController.prototype.renderEditorPlaylists = function(event) {
    var safeEvent = normalizeEvent(event || this.editorEvent || {}, this.state.timezone);
    var currentEventId = asText(safeEvent.id || safeEvent.eventId);
    var canEdit = this.isCurrentEditorEventSaved() && !safeEvent.readOnly;
    var playlists = safeArray(safeEvent.linkedPlaylists);
    if (playlists.length === 0) {
      this.editorPlaylists.innerHTML = '<div class="zy-calendar-panel-copy">No linked playlists yet.</div>';
      return;
    }

    this.editorPlaylists.innerHTML = playlists.map(function(playlist) {
      var safePlaylist = safeObject(playlist);
      var playlistId = asText(safePlaylist.playlistId);
      var builderUrl = asText(safePlaylist.builderUrl);
      var title = asText(safePlaylist.title) || 'Playlist';
      var purpose = asText(safePlaylist.purpose) || 'Playlist';
      var status = asText(safePlaylist.status);
      var connectedEvents = safeArray(safePlaylist.connectedEvents);
      var usageCount = Math.max(0, parseInt(safePlaylist.connectedEventCount || connectedEvents.length || 0, 10) || 0);
      var scoreCount = Math.max(0, parseInt(safePlaylist.totalScores || 0, 10) || 0);
      var usageLabel = 'Used in ' + usageCount + ' ' + pluralize(usageCount, 'event');
      if (safePlaylist.isPrimaryEvent) {
        usageLabel += ' | Primary here';
      }
      var metaText = purpose + ' | ' + scoreCount + ' ' + pluralize(scoreCount, 'song') + ' | ' + usageLabel;
      var eventChips = connectedEvents.length > 0
        ? ('<div class="zy-calendar-playlist-events">' + connectedEvents.map(function(connectedEvent) {
          var safeConnectedEvent = safeObject(connectedEvent);
          var eventId = asText(safeConnectedEvent.eventId);
          var label = formatConnectionLabel(safeConnectedEvent);
          if (eventId === currentEventId) {
            label = 'This event | ' + label;
          }
          if (safeConnectedEvent.isPrimary) {
            label += ' | Primary';
          }
          var eventUrl = asText(safeConnectedEvent.eventUrl);
          return eventUrl !== ''
            ? '<a class="zy-calendar-playlist-event-chip" href="' + escapeHtml(eventUrl) + '" target="_blank" rel="noopener">' + escapeHtml(label) + '</a>'
            : '<span class="zy-calendar-playlist-event-chip">' + escapeHtml(label) + '</span>';
        }).join('') + '</div>')
        : '';

      return ''
        + '<article class="zy-calendar-playlist-card">'
        + '<div class="zy-calendar-playlist-card-head">'
        + '<div>'
        + (builderUrl !== ''
          ? '<a class="zy-calendar-playlist-title" href="' + escapeHtml(builderUrl) + '" target="_blank" rel="noopener">' + escapeHtml(title) + '</a>'
          : '<div class="zy-calendar-playlist-title">' + escapeHtml(title) + '</div>')
        + '<div class="zy-calendar-playlist-meta">' + escapeHtml(metaText) + '</div>'
        + '</div>'
        + (status !== '' ? '<span class="zy-calendar-chip zy-calendar-chip-muted">' + escapeHtml(status) + '</span>' : '')
        + '</div>'
        + eventChips
        + '<div class="zy-calendar-playlist-result-actions">'
        + (builderUrl !== '' ? '<a class="zy-calendar-button" href="' + escapeHtml(builderUrl) + '" target="_blank" rel="noopener">Open</a>' : '')
        + (canEdit ? '<button type="button" class="zy-calendar-button zy-calendar-button-danger" data-action="unlink-playlist" data-playlist-id="' + escapeHtml(playlistId) + '">Unlink</button>' : '')
        + '</div>'
        + '</article>';
    }, this).join('');
  };

  CalendarController.prototype.renderPlaylistSearchResults = function() {
    if (!this.editorPlaylistSearchShell || !this.editorPlaylistResults || !this.editorPlaylistSearchNote) {
      return;
    }

    var canLinkPlaylists = this.supportsPlaylistLinking();
    this.editorPlaylistSearchShell.style.display = canLinkPlaylists ? '' : 'none';
    if (!canLinkPlaylists) {
      return;
    }

    var isSavedEvent = this.isCurrentEditorEventSaved();
    var isReadOnly = !!(this.editorEvent && this.editorEvent.readOnly);
    var query = asText(this.editorPlaylistSearchInput && this.editorPlaylistSearchInput.value);
    if (this.editorPlaylistSearchInput) {
      this.editorPlaylistSearchInput.disabled = !isSavedEvent || isReadOnly;
    }

    if (!isSavedEvent) {
      this.editorPlaylistSearchNote.textContent = 'Save the event first, then reopen it to connect playlists.';
      this.editorPlaylistResults.innerHTML = '';
      return;
    }

    if (isReadOnly) {
      this.editorPlaylistSearchNote.textContent = 'Read-only events cannot change playlist connections.';
      this.editorPlaylistResults.innerHTML = '';
      return;
    }

    this.editorPlaylistSearchNote.textContent = query === ''
      ? 'Search or leave the box empty to load recent playlists.'
      : ('Results for "' + query + '".');
    if (this.editorPlaylistSearchLoading) {
      this.editorPlaylistResults.innerHTML = '<div class="zy-calendar-panel-copy">Searching playlists...</div>';
      return;
    }

    var results = safeArray(this.editorPlaylistResultsData);
    if (results.length === 0) {
      this.editorPlaylistResults.innerHTML = query === ''
        ? '<div class="zy-calendar-panel-copy">No playlists available yet.</div>'
        : '<div class="zy-calendar-panel-copy">No playlists matched your search.</div>';
      return;
    }

    var currentEventId = asText(this.editorEvent && (this.editorEvent.id || this.editorEvent.eventId));
    this.editorPlaylistResults.innerHTML = results.map(function(playlist) {
      var safePlaylist = safeObject(playlist);
      var playlistId = asText(safePlaylist.playlistId);
      var builderUrl = asText(safePlaylist.builderUrl);
      var title = asText(safePlaylist.title) || 'Playlist';
      var purpose = asText(safePlaylist.purpose) || 'Playlist';
      var subtitle = asText(safePlaylist.subtitle);
      var connectedEvents = safeArray(safePlaylist.connectedEvents);
      var usageCount = Math.max(0, parseInt(safePlaylist.connectedEventCount || connectedEvents.length || 0, 10) || 0);
      var alreadyLinked = this.isPlaylistLinkedToEditorEvent(playlistId)
        || connectedEvents.some(function(connectedEvent) {
          return asText(safeObject(connectedEvent).eventId) === currentEventId;
        });
      var metaText = purpose + ' | ' + usageCount + ' ' + pluralize(usageCount, 'event');
      if (subtitle !== '') {
        metaText += ' | ' + subtitle;
      }
      var eventPreview = connectedEvents.length > 0
        ? ('<div class="zy-calendar-playlist-events">' + connectedEvents.slice(0, 3).map(function(connectedEvent) {
          var safeConnectedEvent = safeObject(connectedEvent);
          var label = formatConnectionLabel(safeConnectedEvent);
          if (safeConnectedEvent.isPrimary) {
            label += ' | Primary';
          }
          return '<span class="zy-calendar-playlist-event-chip">' + escapeHtml(label) + '</span>';
        }).join('') + '</div>')
        : '';

      return ''
        + '<article class="zy-calendar-playlist-result">'
        + '<div class="zy-calendar-playlist-result-head">'
        + '<div>'
        + (builderUrl !== ''
          ? '<a class="zy-calendar-playlist-title" href="' + escapeHtml(builderUrl) + '" target="_blank" rel="noopener">' + escapeHtml(title) + '</a>'
          : '<div class="zy-calendar-playlist-title">' + escapeHtml(title) + '</div>')
        + '<div class="zy-calendar-playlist-meta">' + escapeHtml(metaText) + '</div>'
        + '</div>'
        + (asText(safePlaylist.status) !== '' ? '<span class="zy-calendar-chip zy-calendar-chip-muted">' + escapeHtml(asText(safePlaylist.status)) + '</span>' : '')
        + '</div>'
        + eventPreview
        + '<div class="zy-calendar-playlist-result-actions">'
        + (alreadyLinked
          ? '<button type="button" class="zy-calendar-button" disabled>Connected</button>'
          : '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="link-playlist" data-playlist-id="' + escapeHtml(playlistId) + '">Connect</button>')
        + (builderUrl !== '' ? '<a class="zy-calendar-button" href="' + escapeHtml(builderUrl) + '" target="_blank" rel="noopener">Open</a>' : '')
        + '</div>'
        + '</article>';
    }, this).join('');
  };

  CalendarController.prototype.requestPlaylistSearch = function(query) {
    if (!this.supportsPlaylistLinking() || !this.isCurrentEditorEventSaved() || !this.editorEvent || this.editorEvent.readOnly) {
      this.editorPlaylistSearchLoading = false;
      this.editorPlaylistResultsData = [];
      this.renderPlaylistSearchResults();
      return Promise.resolve([]);
    }

    var self = this;
    var safeQuery = asText(query);
    var token = this.editorPlaylistSearchToken + 1;
    this.editorPlaylistSearchToken = token;
    this.editorPlaylistSearchLoading = true;
    this.renderPlaylistSearchResults();
    return Promise.resolve(this.options.onPlaylistSearch(safeQuery, {
      event: this.editorEvent,
      view: this.state.view,
      selectedDate: this.state.selectedDateKey,
      timezone: this.state.timezone
    })).then(function(results) {
      if (token !== self.editorPlaylistSearchToken) {
        return [];
      }

      self.editorPlaylistSearchLoading = false;
      self.editorPlaylistResultsData = safeArray(results);
      self.renderPlaylistSearchResults();
      return self.editorPlaylistResultsData;
    }).catch(function(error) {
      if (token !== self.editorPlaylistSearchToken) {
        return [];
      }

      self.editorPlaylistSearchLoading = false;
      self.editorPlaylistResultsData = [];
      self.renderPlaylistSearchResults();
      self.setEditorMessage(error && error.message ? error.message : 'Playlist search failed.', 'error');
      return [];
    });
  };

  CalendarController.prototype.schedulePlaylistSearch = function(query, immediate) {
    var self = this;
    if (this.editorPlaylistSearchTimer) {
      window.clearTimeout(this.editorPlaylistSearchTimer);
      this.editorPlaylistSearchTimer = 0;
    }

    var safeQuery = asText(query);
    if (immediate) {
      return this.requestPlaylistSearch(safeQuery);
    }

    this.editorPlaylistSearchTimer = window.setTimeout(function() {
      self.editorPlaylistSearchTimer = 0;
      self.requestPlaylistSearch(safeQuery);
    }, 180);
    return Promise.resolve([]);
  };

  CalendarController.prototype.openPlaylistChoiceDialog = function(playlist) {
    var safePlaylist = safeObject(playlist);
    var title = asText(safePlaylist.title) || 'playlist';
    var connectedEvents = safeArray(safePlaylist.connectedEvents);
    var usageCount = Math.max(0, parseInt(safePlaylist.connectedEventCount || connectedEvents.length || 0, 10) || 0);
    this.pendingPlaylistChoice = safePlaylist;
    this.playlistChoiceTitle.textContent = 'Use "' + title + '" or make a copy';
    this.playlistChoiceCopy.textContent = 'This playlist is already connected to ' + usageCount + ' ' + pluralize(usageCount, 'event') + '. Reuse it directly, or create an independent copy for this event.';
    this.playlistChoiceBackdrop.classList.add('is-open');
    this.playlistChoiceBackdrop.setAttribute('aria-hidden', 'false');
  };

  CalendarController.prototype.closePlaylistChoiceDialog = function() {
    this.pendingPlaylistChoice = null;
    if (!this.playlistChoiceBackdrop) {
      return;
    }

    this.playlistChoiceBackdrop.classList.remove('is-open');
    this.playlistChoiceBackdrop.setAttribute('aria-hidden', 'true');
  };

  CalendarController.prototype.runPlaylistMutation = function(callback, successMessage) {
    var self = this;
    this.state.busy = true;
    this.refreshUi();
    return Promise.resolve(callback()).then(function(result) {
      self.state.busy = false;
      self.refreshUi();
      self.closePlaylistChoiceDialog();
      var updatedEvent = result && result.event ? result.event : result;
      if (updatedEvent) {
        var normalized = self.upsertEvent(updatedEvent);
        self.editorEvent = normalized;
        self.renderEditorPlaylists(normalized);
      }
      self.setEditorMessage(successMessage, 'success');
      self.schedulePlaylistSearch(asText(self.editorPlaylistSearchInput && self.editorPlaylistSearchInput.value), true);
      return updatedEvent;
    }).catch(function(error) {
      self.state.busy = false;
      self.refreshUi();
      self.setEditorMessage(error && error.message ? error.message : 'Playlist update failed.', 'error');
      throw error;
    });
  };

  CalendarController.prototype.openEditor = function(event, mode) {
    var safeEvent = normalizeEvent(event || buildDefaultEvent(this.state.timezone, this.state.locale, this.state.selectedDateKey, 9 * 60, false), this.state.timezone);
    this.editorMode = asText(mode) || (safeEvent.id ? 'edit' : 'create');
    this.editorEvent = safeEvent;
    this.host.querySelector('[data-role="editor-kicker"]').textContent = this.editorMode === 'create' ? 'Create event' : 'Edit event';
    this.host.querySelector('[data-role="editor-title"]').textContent = this.editorMode === 'create' ? 'Create event' : safeEvent.title;
    this.editorFields.eventId.value = safeEvent.id;
    this.editorFields.title.value = safeEvent.title;
    this.editorFields.category.value = safeEvent.category;
    this.editorFields.type.value = safeEvent.eventType;
    this.editorFields.status.value = safeEvent.status;
    this.editorFields.start.value = toLocalInputValue(safeEvent.startUtc, safeEvent.timezone, this.state.locale);
    this.editorFields.end.value = toLocalInputValue(safeEvent.endUtc, safeEvent.timezone, this.state.locale);
    this.editorFields.timezone.value = safeEvent.timezone;
    this.editorFields.color.value = safeEvent.color;
    this.editorFields.allDay.checked = !!safeEvent.allDay;
    this.editorFields.readOnly.checked = !!safeEvent.readOnly;
    this.editorFields.location.value = safeEvent.locationLabel;
    this.editorFields.address.value = safeEvent.locationAddress;
    this.editorFields.customerName.value = safeEvent.customerName;
    this.editorFields.customerEmail.value = safeEvent.customerEmail;
    this.editorFields.customerPhone.value = safeEvent.customerPhone;
    this.editorFields.priceAmount.value = safeEvent.priceAmount === null ? '' : String(safeEvent.priceAmount);
    this.editorFields.currency.value = safeEvent.currency;
    this.editorFields.description.value = safeEvent.description;
    this.editorFields.notes.value = safeEvent.notes;
    this.editorFields.logistics.value = safeEvent.logisticsNote;
    this.editorPlaylistResultsData = [];
    this.editorPlaylistSearchLoading = false;
    if (this.editorPlaylistSearchInput) {
      this.editorPlaylistSearchInput.value = '';
    }
    this.closePlaylistChoiceDialog();
    this.renderEditorPlaylists(safeEvent);
    this.setEditorMessage('', 'info');
    this.toggleEditorFields(!safeEvent.readOnly);
    this.renderPlaylistSearchResults();
    this.modalBackdrop.classList.add('is-open');
    this.modalBackdrop.style.display = 'flex';
    this.modalBackdrop.setAttribute('aria-hidden', 'false');
    if (this.supportsPlaylistLinking() && this.isCurrentEditorEventSaved() && !safeEvent.readOnly) {
      this.schedulePlaylistSearch('', true);
    }
    this.editorFields.title.focus();
  };

  CalendarController.prototype.closeEditor = function() {
    this.modalBackdrop.classList.remove('is-open');
    this.modalBackdrop.style.display = 'none';
    this.modalBackdrop.setAttribute('aria-hidden', 'true');
    this.closePlaylistChoiceDialog();
    if (this.editorPlaylistSearchTimer) {
      window.clearTimeout(this.editorPlaylistSearchTimer);
      this.editorPlaylistSearchTimer = 0;
    }
    this.editorPlaylistResultsData = [];
    this.editorPlaylistSearchLoading = false;
    this.editorEvent = null;
    this.editorMode = '';
  };

  CalendarController.prototype.toggleEditorFields = function(enabled) {
    var self = this;
    Object.keys(this.editorFields).forEach(function(key) {
      if (key === 'readOnly') {
        return;
      }
      self.editorFields[key].disabled = !enabled;
    });
    if (this.editorPlaylistSearchInput) {
      this.editorPlaylistSearchInput.disabled = !enabled || !this.isCurrentEditorEventSaved();
    }
    this.host.querySelector('[data-action="delete-event"]').disabled = !enabled || !this.options.allowDelete;
  };

  CalendarController.prototype.setEditorMessage = function(message, tone) {
    var safeMessage = asText(message);
    this.editorMessage.textContent = safeMessage;
    this.editorMessage.classList.toggle('is-visible', safeMessage !== '');
    this.editorMessage.classList.toggle('is-error', tone === 'error');
    this.editorMessage.classList.toggle('is-success', tone === 'success');
  };

  CalendarController.prototype.editorValue = function() {
    var timezone = asText(this.editorFields.timezone.value) || this.state.timezone;
    var draft = normalizeEvent(Object.assign({}, this.editorEvent || {}, {
      id: asText(this.editorFields.eventId.value) || asText(this.editorEvent && this.editorEvent.id),
      eventId: asText(this.editorFields.eventId.value) || asText(this.editorEvent && this.editorEvent.id),
      title: asText(this.editorFields.title.value),
      category: asText(this.editorFields.category.value),
      eventType: asText(this.editorFields.type.value),
      status: asText(this.editorFields.status.value),
      startUtc: localInputToUtcIso(this.editorFields.start.value, timezone, this.state.locale),
      endUtc: localInputToUtcIso(this.editorFields.end.value, timezone, this.state.locale),
      timezone: timezone,
      color: asText(this.editorFields.color.value),
      allDay: this.editorFields.allDay.checked,
      readOnly: this.editorFields.readOnly.checked,
      locationLabel: asText(this.editorFields.location.value),
      locationAddress: asText(this.editorFields.address.value),
      customerName: asText(this.editorFields.customerName.value),
      customerEmail: asText(this.editorFields.customerEmail.value),
      customerPhone: asText(this.editorFields.customerPhone.value),
      priceAmount: asText(this.editorFields.priceAmount.value),
      currency: asText(this.editorFields.currency.value).toUpperCase() || 'USD',
      description: asText(this.editorFields.description.value),
      notes: asText(this.editorFields.notes.value),
      logisticsNote: asText(this.editorFields.logistics.value)
    }), timezone);
    return draft;
  };

  CalendarController.prototype.upsertEvent = function(event) {
    var normalized = normalizeEvent(event, this.state.timezone);
    var replaced = false;
    this.state.events = this.state.events.map(function(existing) {
      if (existing.id === normalized.id || existing.eventId === normalized.id || existing.id === normalized.eventId) {
        replaced = true;
        return normalized;
      }
      return existing;
    });
    if (!replaced) {
      this.state.events.push(normalized);
    }
    this.state.events.sort(compareEvents);
    this.state.visibleEvents = this.getVisibleEvents();
    this.selectEventById(normalized.id, false);
    this.refreshUi();
    this.scheduleRender();
    return normalized;
  };

  CalendarController.prototype.removeEventById = function(eventId) {
    var safeId = asText(eventId);
    this.state.events = this.state.events.filter(function(event) {
      return event.id !== safeId && event.eventId !== safeId;
    });
    if (this.state.selectedEventId === safeId) {
      this.state.selectedEventId = '';
      this.state.selectedEvent = null;
    }
    this.state.visibleEvents = this.getVisibleEvents();
    this.refreshUi();
    this.scheduleRender();
  };

  CalendarController.prototype.persistEvent = function(mode, event) {
    var self = this;
    var callback = mode === 'create' ? this.options.onEventCreate : this.options.onEventUpdate;
    if (typeof callback !== 'function') {
      return Promise.resolve(this.upsertEvent(event));
    }
    this.state.busy = true;
    this.refreshUi();
    return Promise.resolve(callback(event, {
      mode: mode,
      view: this.state.view,
      selectedDate: this.state.selectedDateKey,
      timezone: this.state.timezone
    })).then(function(result) {
      self.state.busy = false;
      self.refreshUi();
      return self.upsertEvent(result || event);
    }).catch(function(error) {
      self.state.busy = false;
      self.refreshUi();
      throw error;
    });
  };

  CalendarController.prototype.persistDelete = function(event) {
    var self = this;
    if (typeof this.options.onEventDelete !== 'function') {
      this.removeEventById(event.id);
      return Promise.resolve();
    }
    this.state.busy = true;
    this.refreshUi();
    return Promise.resolve(this.options.onEventDelete(event, {
      view: this.state.view,
      selectedDate: this.state.selectedDateKey,
      timezone: this.state.timezone
    })).then(function() {
      self.state.busy = false;
      self.removeEventById(event.id);
      self.refreshUi();
    }).catch(function(error) {
      self.state.busy = false;
      self.refreshUi();
      throw error;
    });
  };

  CalendarController.prototype.onToolbarClick = function(event) {
    var source = event.target;
    if (!(source instanceof Element)) {
      return;
    }

    var target = source.closest('[data-action], [data-view], [data-scope]');
    if (!(target instanceof HTMLElement)) {
      return;
    }

    var action = asText(target.getAttribute('data-action'));
    var view = asText(target.getAttribute('data-view'));
    var scope = asText(target.getAttribute('data-scope'));
    if (view !== '' || scope !== '' || (action !== '' && action !== 'toggle-export-menu')) {
      this.setToolbarMenuOpen(false);
    }
    if (view !== '') {
      this.setView(view, true);
      return;
    }
    if (scope !== '') {
      this.state.listScope = scope;
      this.state.visibleEvents = this.getVisibleEvents(scope);
      this.refreshUi();
      return;
    }
    if (action === 'today') {
      this.selectDate(DateMath.todayKey(), true);
      return;
    }
    if (action === 'previous') {
      this.shiftRange(-1);
      return;
    }
    if (action === 'next') {
      this.shiftRange(1);
      return;
    }
    if (action === 'open-help') {
      this.openUtilityModal('help');
      return;
    }
    if (action === 'open-settings') {
      this.openUtilityModal('settings');
      return;
    }
    if (action === 'toggle-export-menu') {
      this.setToolbarMenuOpen(!this.toolbarMenuOpen);
      return;
    }
    if (action === 'add-event') {
      if (!this.options.allowCreate) {
        return;
      }

      this.closeUtilityModal();
      this.openEditor(buildDefaultEvent(this.state.timezone, this.state.locale, this.state.selectedDateKey, 9 * 60, false), 'create');
      return;
    }
    if (action === 'export-csv') {
      this.setToolbarMenuOpen(false);
      this.requestExport('csv');
      return;
    }
    if (action === 'export-xlsx') {
      this.setToolbarMenuOpen(false);
      this.requestExport('xlsx');
    }
  };

  CalendarController.prototype.requestExport = function(format) {
    if (typeof this.options.onExportRequest !== 'function') {
      this.setMessage('Export callback is not configured.', 'error');
      return;
    }
    var visible = this.getVisibleEvents(this.state.view === 'list' ? this.state.listScope : this.state.view);
    this.options.onExportRequest(asText(format), visible, {
      view: this.state.view,
      scope: this.state.view === 'list' ? this.state.listScope : this.state.view,
      selectedDate: this.state.selectedDateKey,
      timezone: this.state.timezone
    });
  };

  CalendarController.prototype.onToolbarChange = function(event) {
    var target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    if (target.getAttribute('data-role') === 'mobile-view-select') {
      this.setToolbarMenuOpen(false);
      this.setView(asText(target.value), true);
    }
  };

  CalendarController.prototype.onPanelClick = function(event) {
    var source = event.target;
    if (!(source instanceof Element)) {
      return;
    }

    var target = source.closest('[data-action], [data-event-id]');
    if (!(target instanceof HTMLElement)) {
      return;
    }

    var action = asText(target.getAttribute('data-action'));
    var eventId = asText(target.getAttribute('data-event-id'));
    if (eventId !== '') {
      this.selectEventById(eventId, true);
    }
    if (action === 'edit-selected' || action === 'edit-row') {
      var selectedEvent = eventId !== '' ? this.state.events.find(function(item) { return item.id === eventId || item.eventId === eventId; }) : this.getSelectedEvent();
      if (selectedEvent && this.options.allowEdit && !selectedEvent.readOnly) {
        this.openEditor(selectedEvent, 'edit');
      }
      return;
    }
    if (action === 'select-row') {
      if (eventId !== '') {
        this.selectEventById(eventId, true);
      }
      return;
    }
    if (action === 'focus-selected') {
      if (this.getSelectedEvent()) {
        this.state.anchorDateKey = getDateKeyFromIso(this.getSelectedEvent().startUtc, this.state.timezone, this.state.locale);
        if (this.state.view === 'list') {
          this.setView(this.state.lastSpatialView || 'week', true);
        } else {
          this.state.visibleEvents = this.getVisibleEvents();
          this.refreshUi();
          this.scheduleRender();
        }
      }
      return;
    }
    if (action === 'delete-selected') {
      var current = this.getSelectedEvent();
      if (current && !current.readOnly && window.confirm('Delete the selected event?')) {
        this.persistDelete(current).catch(function(error) {
          this.setMessage(error && error.message ? error.message : 'Delete failed.', 'error');
        }.bind(this));
      }
      return;
    }
    if (action === 'go-list') {
      this.setView('list', true);
    }
  };

  CalendarController.prototype.onModalClick = function(event) {
    var target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    if (target === this.modalBackdrop) {
      this.closeEditor();
      return;
    }
    if (target === this.playlistChoiceBackdrop) {
      this.closePlaylistChoiceDialog();
      return;
    }

    var action = asText(target.getAttribute('data-action'));
    if (action === 'close-editor') {
      this.closeEditor();
      return;
    }
    if (action === 'link-playlist') {
      var candidate = this.findPlaylistSearchResultById(asText(target.getAttribute('data-playlist-id')));
      if (!candidate) {
        this.setEditorMessage('Playlist selection is no longer available. Search again.', 'error');
        return;
      }

      var connectedEvents = safeArray(safeObject(candidate).connectedEvents).filter(function(connectedEvent) {
        return asText(safeObject(connectedEvent).eventId) !== asText(this.editorEvent && (this.editorEvent.id || this.editorEvent.eventId));
      }, this);
      if (connectedEvents.length > 0) {
        this.openPlaylistChoiceDialog(candidate);
        return;
      }

      this.runPlaylistMutation(function() {
        return this.options.onPlaylistLink(this.editorEvent, candidate, {
          view: this.state.view,
          selectedDate: this.state.selectedDateKey,
          timezone: this.state.timezone
        });
      }.bind(this), 'Playlist connected.');
      return;
    }
    if (action === 'unlink-playlist') {
      var linkedPlaylist = this.findLinkedPlaylistById(asText(target.getAttribute('data-playlist-id')));
      if (!linkedPlaylist) {
        this.setEditorMessage('Linked playlist was not found.', 'error');
        return;
      }

      this.runPlaylistMutation(function() {
        return this.options.onPlaylistUnlink(this.editorEvent, linkedPlaylist, {
          view: this.state.view,
          selectedDate: this.state.selectedDateKey,
          timezone: this.state.timezone
        });
      }.bind(this), 'Playlist disconnected.');
      return;
    }
    if (action === 'playlist-choice-cancel') {
      this.closePlaylistChoiceDialog();
      return;
    }
    if (action === 'playlist-choice-direct') {
      if (!this.pendingPlaylistChoice) {
        return;
      }

      this.runPlaylistMutation(function() {
        return this.options.onPlaylistLink(this.editorEvent, this.pendingPlaylistChoice, {
          view: this.state.view,
          selectedDate: this.state.selectedDateKey,
          timezone: this.state.timezone
        });
      }.bind(this), 'Playlist connected.');
      return;
    }
    if (action === 'playlist-choice-copy') {
      if (!this.pendingPlaylistChoice) {
        return;
      }

      this.runPlaylistMutation(function() {
        return this.options.onPlaylistClone(this.editorEvent, this.pendingPlaylistChoice, {
          view: this.state.view,
          selectedDate: this.state.selectedDateKey,
          timezone: this.state.timezone
        });
      }.bind(this), 'Playlist copy created and connected.');
      return;
    }
    if (action === 'delete-event') {
      if (this.editorEvent && this.editorEvent.id && !this.editorEvent.readOnly && window.confirm('Delete this event?')) {
        this.persistDelete(this.editorEvent).then(function() {
          this.closeEditor();
          this.setMessage('Event deleted.', 'success');
        }.bind(this)).catch(function(error) {
          this.setEditorMessage(error && error.message ? error.message : 'Delete failed.', 'error');
        }.bind(this));
      }
    }
  };

  CalendarController.prototype.onUtilityClick = function(event) {
    var target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    if (target === this.utilityBackdrop) {
      this.closeUtilityModal();
      return;
    }

    var actionTarget = target.closest('[data-action]');
    if (!(actionTarget instanceof HTMLElement)) {
      return;
    }

    var action = asText(actionTarget.getAttribute('data-action'));
    if (action === 'close-utility') {
      this.closeUtilityModal();
      return;
    }
    if (action === 'apply-utility-settings') {
      var timezoneInput = this.utilityBackdrop.querySelector('[data-role="utility-timezone-input"]');
      var nextTimezone = timezoneInput instanceof HTMLInputElement ? timezoneInput.value : this.state.timezone;
      this.applyTimezone(nextTimezone);
      this.closeUtilityModal();
      this.setMessage('Display timezone updated.', 'success');
    }
  };

  CalendarController.prototype.onWindowPointerDown = function(event) {
    if (!this.toolbarMenuOpen || !this.toolbarMenuShell) {
      return;
    }

    var target = event.target;
    if (target instanceof Node && this.toolbarMenuShell.contains(target)) {
      return;
    }
    this.setToolbarMenuOpen(false);
  };

  CalendarController.prototype.onModalChange = function(event) {
    var target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    if (target.getAttribute('data-role') === 'editor-read-only') {
      this.toggleEditorFields(!this.editorFields.readOnly.checked);
      this.renderPlaylistSearchResults();
    }
  };

  CalendarController.prototype.onModalInput = function(event) {
    var target = event.target;
    if (!(target instanceof HTMLElement)) {
      return;
    }

    if (target.getAttribute('data-role') === 'editor-playlist-search') {
      this.schedulePlaylistSearch(asText(target.value), false);
    }
  };

  CalendarController.prototype.onModalSubmit = function(event) {
    event.preventDefault();
    var draft = this.editorValue();
    if (draft.title === '') {
      this.setEditorMessage('Title is required.', 'error');
      return;
    }
    if (draft.startUtc === '' || draft.endUtc === '') {
      this.setEditorMessage('Start and end are required.', 'error');
      return;
    }
    if (new Date(draft.endUtc).getTime() <= new Date(draft.startUtc).getTime()) {
      this.setEditorMessage('End must be after start.', 'error');
      return;
    }

    var mode = this.editorMode === 'create' || draft.id === '' ? 'create' : 'update';
    this.persistEvent(mode === 'create' ? 'create' : 'update', draft).then(function(result) {
      this.closeEditor();
      this.setMessage(mode === 'create' ? 'Event created.' : 'Event updated.', 'success');
      this.selectEventById(result.id, true);
    }.bind(this)).catch(function(error) {
      this.setEditorMessage(error && error.message ? error.message : 'Save failed.', 'error');
    }.bind(this));
  };

})();
