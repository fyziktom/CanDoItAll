(function() {
  if (window.ZyCanvasCalendar) {
    return;
  }
  var shared = window.ZyCanvasCalendarModule;
  if (!shared) { throw new Error('ZyCanvasCalendar foundation must load before 02-controller-and-dom.js.'); }
  var { CanvasSurface, HitRegistry, DateMath, drawMiniMonth, drawTimedGrid, fillRoundedPanel, fitText, wrapText, STYLE_ID, DAY_SHORT, MONTH_SHORT, TIMEZONE_FALLBACKS, injectStyles, asText, asNumber, clamp, safeObject, safeArray, copy, escapeHtml, padNumber, ensureDateKey, normalizeIsoString, minutesToClockLabel, formatterKey, getFormatter, getZonedParts, zonedPartsToDateKey, getDateKeyFromIso, getMinutesFromIso, formatDateKeyLabel, formatDateTimeLabel, formatRangeLabel, formatRangeLabelLines, renderListRangeLabel, renderCalendarActionIcon, renderCalendarToolbarIcon, renderCalendarListActionButton, renderCalendarToolbarIconButton, renderCalendarExportMenuItem, renderCalendarAddEventButton, toLocalInputValue, parseLocalInputValue, zonedLocalToUtcIso, localInputToUtcIso, buildUtcIsoFromDateKeyMinutes, addMinutesToIso, addDaysToIso, durationMinutes, createLocalEventId, normalizeEvent, pluralize, formatConnectionLabel, compareEvents, getEventSpan, compareDateKeys, eventSpansDate, eventIntersectsRange, buildDensityMap, buildTimeZoneList, buildDefaultEvent, formatPeriodLabel, scopeRange } = shared;
  function CalendarController(options) {
    injectStyles();

    var settings = safeObject(options);
    if (!(settings.host instanceof HTMLElement)) {
      throw new Error('ZyCanvasCalendar requires a host element.');
    }

    this.host = settings.host;
    this.options = Object.assign({
      initialView: 'week',
      selectedDate: DateMath.todayKey(),
      selectedEventId: '',
      timezone: 'UTC',
      locale: navigator.language || 'en-US',
      weekStartsOn: 1,
      slotMinutes: 30,
      businessHoursStart: 7,
      businessHoursEnd: 22,
      miniMonthCount: 2,
      allowCreate: true,
      allowEdit: true,
      allowDelete: true,
      allowDragDrop: true,
      allowResize: true,
      enableListExport: true,
      eventTypes: ['Concert', 'Wedding', 'Ceremony', 'Gig', 'Practice', 'Other'],
      eventStatuses: ['Draft', 'Planned', 'Confirmed', 'Completed', 'Cancelled', 'Archived'],
      timeZoneOptions: [],
      emptyMessage: 'No events in the visible range.',
      onEventCreate: null,
      onEventUpdate: null,
      onEventDelete: null,
      onPlaylistSearch: null,
      onPlaylistLink: null,
      onPlaylistClone: null,
      onPlaylistUnlink: null,
      onDateChange: null,
      onViewChange: null,
      onTimezoneChange: null,
      onSelectionChange: null,
      onExportRequest: null,
      workspaceModal: false
    }, settings);
    this.state = {
      view: asText(this.options.initialView) || 'week',
      lastSpatialView: 'week',
      listScope: asText(this.options.initialView) === 'list' ? 'week' : (asText(this.options.initialView) || 'week'),
      selectedDateKey: ensureDateKey(this.options.selectedDate),
      anchorDateKey: ensureDateKey(this.options.selectedDate),
      timezone: asText(this.options.timezone) || 'UTC',
      locale: asText(this.options.locale) || 'en-US',
      hoveredRegion: null,
      selectedEventId: asText(this.options.selectedEventId),
      focusedDateKey: ensureDateKey(this.options.selectedDate),
      interaction: null,
      busy: false,
      message: '',
      messageTone: 'info',
      layoutCache: {},
      visibleEvents: [],
      selectedEvent: null,
      events: []
    };
    if (this.state.view !== 'list') {
      this.state.lastSpatialView = this.state.view;
    }
    this.toolbarMenuOpen = false;
    this.utilityModalKind = '';

    this.registry = new HitRegistry();
    this.frameHandle = 0;
    this.renderBound = this.render.bind(this);
    this.handleResize = this.scheduleRender.bind(this);
    this.handleCanvasPointerDown = this.onCanvasPointerDown.bind(this);
    this.handleCanvasPointerMove = this.onCanvasPointerMove.bind(this);
    this.handleCanvasPointerUp = this.onCanvasPointerUp.bind(this);
    this.handleCanvasLeave = this.onCanvasLeave.bind(this);
    this.handleCanvasDblClick = this.onCanvasDoubleClick.bind(this);
    this.handleCanvasKeyDown = this.onCanvasKeyDown.bind(this);
    this.handleToolbarClick = this.onToolbarClick.bind(this);
    this.handleToolbarChange = this.onToolbarChange.bind(this);
    this.handlePanelClick = this.onPanelClick.bind(this);
    this.handleModalSubmit = this.onModalSubmit.bind(this);
    this.handleModalClick = this.onModalClick.bind(this);
    this.handleModalChange = this.onModalChange.bind(this);
    this.handleModalInput = this.onModalInput.bind(this);
    this.handleUtilityClick = this.onUtilityClick.bind(this);
    this.handleWindowPointerDown = this.onWindowPointerDown.bind(this);
    this.handleWindowPointerMove = this.onWindowPointerMove.bind(this);
    this.handleWindowPointerUp = this.onWindowPointerUp.bind(this);
    this.editorPlaylistResultsData = [];
    this.editorPlaylistSearchLoading = false;
    this.editorPlaylistSearchToken = 0;
    this.editorPlaylistSearchTimer = 0;
    this.pendingPlaylistChoice = null;
    this.buildDom();
    this.surface = new CanvasSurface({
      canvas: this.canvas,
      resizeTarget: this.canvasShell,
      onResize: this.handleResize
    });
    this.bindEvents();
    this.setEvents(safeArray(this.options.events));
    if (this.state.selectedEventId !== '') {
      this.selectEventById(this.state.selectedEventId, false);
    }
    this.refreshUi();
    this.scheduleRender();
  }

  CalendarController.prototype.buildDom = function() {
    var timeZoneOptions = buildTimeZoneList(this.state.timezone, this.options.timeZoneOptions);
    var timeZoneOptionsHtml = timeZoneOptions.map(function(value) {
      return '<option value="' + escapeHtml(value) + '"></option>';
    }).join('');
    var eventTypeOptionsHtml = safeArray(this.options.eventTypes).map(function(value) {
      return '<option value="' + escapeHtml(value) + '">' + escapeHtml(value) + '</option>';
    }).join('');
    var eventStatusOptionsHtml = safeArray(this.options.eventStatuses).map(function(value) {
      return '<option value="' + escapeHtml(value) + '">' + escapeHtml(value) + '</option>';
    }).join('');
    var shellClassName = 'zy-calendar-shell' + (this.options.workspaceModal ? ' zy-calendar-shell-workspace' : '');
    this.host.classList.toggle('zy-calendar-host-workspace', !!this.options.workspaceModal);

    this.host.innerHTML = ''
      + '<div class="' + shellClassName + '">'
      + '<div class="zy-calendar-body">'
      + '<div class="zy-calendar-stage">'
      + '<div class="zy-calendar-stage-shell">'
      + '<div class="zy-calendar-toolbar" data-role="toolbar">'
      + '<div class="zy-calendar-toolbar-group">'
      + '<button type="button" class="zy-calendar-button" data-action="today">Today</button>'
      + '<button type="button" class="zy-calendar-button" data-action="previous" aria-label="Previous range" title="Previous range"><span class="zy-calendar-toolbar-icon zy-calendar-toolbar-nav-icon" aria-hidden="true">&lsaquo;</span></button>'
      + '<button type="button" class="zy-calendar-button" data-action="next" aria-label="Next range" title="Next range"><span class="zy-calendar-toolbar-icon zy-calendar-toolbar-nav-icon" aria-hidden="true">&rsaquo;</span></button>'
      + '</div>'
      + '<span class="zy-calendar-toolbar-divider" aria-hidden="true">|</span>'
      + '<div class="zy-calendar-toolbar-group zy-calendar-toolbar-meta">'
      + '<div class="zy-calendar-period-label" data-role="period-label">Calendar</div>'
      + '<div class="zy-calendar-period-subtitle" data-role="period-subtitle"></div>'
      + '</div>'
      + '<span class="zy-calendar-toolbar-divider" aria-hidden="true">|</span>'
      + '<div class="zy-calendar-toolbar-group">'
      + '<div class="zy-calendar-view-switcher" data-role="view-switcher">'
      + '<button type="button" class="zy-calendar-view-button" data-view="day">Day</button>'
      + '<button type="button" class="zy-calendar-view-button" data-view="week">Week</button>'
      + '<button type="button" class="zy-calendar-view-button" data-view="month">Month</button>'
      + '<button type="button" class="zy-calendar-view-button" data-view="year">Year</button>'
      + '<button type="button" class="zy-calendar-view-button" data-view="list">List</button>'
      + '</div>'
      + '<label class="zy-calendar-mobile-view-field" aria-label="Calendar view">'
      + '<select class="zy-calendar-toolbar-input zy-calendar-mobile-view-select" data-role="mobile-view-select" aria-label="Calendar view">'
      + '<option value="day">Day</option>'
      + '<option value="week">Week</option>'
      + '<option value="month">Month</option>'
      + '<option value="year">Year</option>'
      + '<option value="list">List</option>'
      + '</select>'
      + '</label>'
      + '</div>'
      + '<span class="zy-calendar-toolbar-divider" aria-hidden="true">|</span>'
      + '<div class="zy-calendar-toolbar-group">'
      + renderCalendarToolbarIconButton('open-help', 'Show help', 'help', false, '')
      + renderCalendarToolbarIconButton('open-settings', 'Open settings', 'settings', false, '')
      + (this.options.allowCreate ? renderCalendarAddEventButton() : '')
      + '</div>'
      + '<span class="zy-calendar-toolbar-divider" aria-hidden="true">|</span>'
      + '<div class="zy-calendar-toolbar-group">'
      + '<div class="zy-calendar-toolbar-menu-shell" data-role="toolbar-menu-shell">'
      + renderCalendarToolbarIconButton('toggle-export-menu', 'Open downloads menu', 'menu', true, 'data-role="toolbar-menu-toggle" aria-haspopup="menu" aria-expanded="false"')
      + '<div class="zy-calendar-toolbar-menu-popover" data-role="toolbar-menu-popover" aria-label="Download visible range" role="menu">'
      + renderCalendarExportMenuItem('csv')
      + renderCalendarExportMenuItem('xlsx')
      + '</div>'
      + '</div>'
      + '</div>'
      + '</div>'
      + '<datalist id="zy-calendar-timezones">' + timeZoneOptionsHtml + '</datalist>'
      + '<div class="zy-calendar-statusbar" data-role="statusbar">'
      + '<div class="zy-calendar-inline-message" data-role="inline-message"></div>'
      + '</div>'
      + '<div class="zy-calendar-canvas-shell" data-role="canvas-shell">'
      + '<div class="zy-calendar-loading" data-role="loading">Working...</div>'
      + '<canvas class="zy-calendar-canvas" data-role="canvas" tabindex="0" aria-label="Canvas calendar"></canvas>'
      + '</div>'
      + '<div class="zy-calendar-list-shell" data-role="list-shell">'
      + '<div class="zy-calendar-list-header">'
      + '<div>'
      + '<div class="zy-calendar-panel-kicker">Visible list</div>'
      + '<h3 class="zy-calendar-panel-title" style="margin:0;" data-role="list-title">Visible events</h3>'
      + '<p class="zy-calendar-panel-copy" data-role="list-copy">Sorted by start time in the selected timezone.</p>'
      + '</div>'
      + '<div class="zy-calendar-scope-switcher" data-role="scope-switcher">'
      + '<button type="button" class="zy-calendar-view-button" data-scope="day">Day</button>'
      + '<button type="button" class="zy-calendar-view-button" data-scope="week">Week</button>'
      + '<button type="button" class="zy-calendar-view-button" data-scope="month">Month</button>'
      + '<button type="button" class="zy-calendar-view-button" data-scope="year">Year</button>'
      + '</div>'
      + '</div>'
      + '<div class="zy-calendar-list-table-wrap" data-role="list-content"></div>'
      + '</div>'
      + '<div class="zy-calendar-utility-backdrop" data-role="utility-backdrop" aria-hidden="true">'
      + '<div class="zy-calendar-utility-dialog" role="dialog" aria-modal="true" aria-labelledby="zy-calendar-utility-title">'
      + '<div class="zy-calendar-utility-header">'
      + '<div>'
      + '<div class="zy-calendar-panel-kicker" data-role="utility-kicker">Help</div>'
      + '<h3 class="zy-calendar-utility-title" id="zy-calendar-utility-title" data-role="utility-title">Canvas help</h3>'
      + '</div>'
      + '<button type="button" class="zy-calendar-button" data-action="close-utility">Close</button>'
      + '</div>'
      + '<div class="zy-calendar-utility-body" data-role="utility-body"></div>'
      + '<div class="zy-calendar-utility-footer" data-role="utility-footer"></div>'
      + '</div>'
      + '</div>'
      + '</div>'
      + '</div>'
      + '<aside class="zy-calendar-panel" data-role="panel">'
      + '<section class="zy-calendar-panel-card">'
      + '<div class="zy-calendar-panel-kicker">Selection</div>'
      + '<h2 class="zy-calendar-panel-title" data-role="panel-title">Visible range</h2>'
      + '<p class="zy-calendar-panel-copy" data-role="panel-copy">Select an event or a date to inspect it.</p>'
      + '<div class="zy-calendar-stat-grid" data-role="panel-stats"></div>'
      + '<div class="zy-calendar-event-meta" data-role="panel-meta"></div>'
      + '<div class="zy-calendar-event-actions" data-role="panel-actions"></div>'
      + '</section>'
      + '</aside>'
      + '</div>'
      + '<div class="zy-calendar-live-region" data-role="live-region" aria-live="polite"></div>'
      + '</div>'
      + '<div class="zy-calendar-backdrop" data-role="modal-backdrop">'
      + '<div class="zy-calendar-editor" role="dialog" aria-modal="true" aria-labelledby="zy-calendar-editor-title">'
      + '<form data-role="editor-form">'
      + '<div class="zy-calendar-editor-header">'
      + '<div>'
      + '<div class="zy-calendar-panel-kicker" data-role="editor-kicker">Event</div>'
      + '<h2 class="zy-calendar-editor-title" id="zy-calendar-editor-title" data-role="editor-title">Edit event</h2>'
      + '</div>'
      + '<button type="button" class="zy-calendar-button" data-action="close-editor">Close</button>'
      + '</div>'
      + '<div class="zy-calendar-editor-body">'
      + '<div class="zy-calendar-inline-message" data-role="editor-message"></div>'
      + '<input type="hidden" name="eventId" data-role="editor-event-id" />'
      + '<div class="zy-calendar-editor-grid">'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Title</span><input class="zy-calendar-editor-input" name="title" data-role="editor-title-input" required /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Category</span><input class="zy-calendar-editor-input" name="category" data-role="editor-category" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Type</span><select class="zy-calendar-editor-select" name="eventType" data-role="editor-type">' + eventTypeOptionsHtml + '</select></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Status</span><select class="zy-calendar-editor-select" name="status" data-role="editor-status">' + eventStatusOptionsHtml + '</select></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Start</span><input class="zy-calendar-editor-input" type="datetime-local" name="startLocal" data-role="editor-start" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">End</span><input class="zy-calendar-editor-input" type="datetime-local" name="endLocal" data-role="editor-end" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Timezone</span><input class="zy-calendar-editor-input" list="zy-calendar-timezones" name="timezoneName" data-role="editor-timezone" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Color</span><input class="zy-calendar-editor-input" type="color" name="color" data-role="editor-color" /></label>'
      + '</div>'
      + '<div class="zy-calendar-editor-inline">'
      + '<label class="zy-calendar-editor-checkbox"><input type="checkbox" name="allDay" data-role="editor-all-day" />All day</label>'
      + '<label class="zy-calendar-editor-checkbox"><input type="checkbox" name="readOnly" data-role="editor-read-only" />Read only</label>'
      + '</div>'
      + '<div class="zy-calendar-editor-grid">'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Location</span><input class="zy-calendar-editor-input" name="locationLabel" data-role="editor-location" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Address</span><input class="zy-calendar-editor-input" name="locationAddress" data-role="editor-address" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Customer</span><input class="zy-calendar-editor-input" name="customerName" data-role="editor-customer-name" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Customer email</span><input class="zy-calendar-editor-input" name="customerEmail" type="email" data-role="editor-customer-email" /></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Customer phone</span><input class="zy-calendar-editor-input" name="customerPhone" data-role="editor-customer-phone" /></label>'
      + '<div class="zy-calendar-editor-inline">'
      + '<label class="zy-calendar-editor-field" style="flex:1 1 0;"><span class="zy-calendar-editor-label">Price</span><input class="zy-calendar-editor-input" name="priceAmount" type="number" step="0.01" data-role="editor-price" /></label>'
      + '<label class="zy-calendar-editor-field" style="width:120px;"><span class="zy-calendar-editor-label">Currency</span><input class="zy-calendar-editor-input" name="currency" maxlength="3" data-role="editor-currency" /></label>'
      + '</div>'
      + '</div>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Description</span><textarea class="zy-calendar-editor-textarea" name="description" data-role="editor-description"></textarea></label>'
      + '<div class="zy-calendar-editor-grid">'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Notes</span><textarea class="zy-calendar-editor-textarea" name="notes" data-role="editor-notes"></textarea></label>'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Logistics</span><textarea class="zy-calendar-editor-textarea" name="logisticsNote" data-role="editor-logistics"></textarea></label>'
      + '</div>'
      + '<div class="zy-calendar-panel-card" style="padding:14px 16px;">'
      + '<div class="zy-calendar-panel-kicker">Linked playlists</div>'
      + '<div class="zy-calendar-playlist-search" data-role="editor-playlist-search-shell">'
      + '<label class="zy-calendar-editor-field"><span class="zy-calendar-editor-label">Find existing playlist</span><input class="zy-calendar-editor-input" type="search" autocomplete="off" placeholder="Search playlists by title, subtitle, or notes" data-role="editor-playlist-search" /></label>'
      + '<div class="zy-calendar-panel-copy" data-role="editor-playlist-search-note">Search to connect an existing playlist to this event.</div>'
      + '<div class="zy-calendar-playlist-results" data-role="editor-playlist-results"></div>'
      + '</div>'
      + '<div class="zy-calendar-playlist-list" data-role="editor-playlists">No linked playlists yet.</div>'
      + '</div>'
      + '</div>'
      + '<div class="zy-calendar-editor-footer">'
      + '<div class="zy-calendar-editor-note" data-role="editor-note">UTC is canonical. Rendering uses the selected display timezone.</div>'
      + '<div class="zy-calendar-editor-inline">'
      + '<button type="button" class="zy-calendar-button zy-calendar-button-danger" data-action="delete-event">Delete</button>'
      + '<button type="button" class="zy-calendar-button" data-action="close-editor">Cancel</button>'
      + '<button type="submit" class="zy-calendar-button zy-calendar-button-primary">Save event</button>'
      + '</div>'
      + '</div>'
      + '</form>'
      + '<div class="zy-calendar-choice-backdrop" data-role="playlist-choice-backdrop" aria-hidden="true">'
      + '<div class="zy-calendar-choice-dialog" role="dialog" aria-modal="true" aria-labelledby="zy-calendar-playlist-choice-title">'
      + '<div class="zy-calendar-panel-kicker">Playlist already in use</div>'
      + '<h3 class="zy-calendar-panel-title" id="zy-calendar-playlist-choice-title" data-role="playlist-choice-title">Choose how to connect it</h3>'
      + '<p class="zy-calendar-panel-copy" data-role="playlist-choice-copy"></p>'
      + '<div class="zy-calendar-editor-inline">'
      + '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="playlist-choice-direct">Use existing playlist</button>'
      + '<button type="button" class="zy-calendar-button" data-action="playlist-choice-copy">Make copy</button>'
      + '<button type="button" class="zy-calendar-button" data-action="playlist-choice-cancel">Cancel</button>'
      + '</div>'
      + '</div>'
      + '</div>'
      + '</div>'
      + '</div>';

    this.toolbar = this.host.querySelector('[data-role="toolbar"]');
    this.statusbar = this.host.querySelector('[data-role="statusbar"]');
    this.canvasShell = this.host.querySelector('[data-role="canvas-shell"]');
    this.canvas = this.host.querySelector('[data-role="canvas"]');
    this.listShell = this.host.querySelector('[data-role="list-shell"]');
    this.listContent = this.host.querySelector('[data-role="list-content"]');
    this.listTitle = this.host.querySelector('[data-role="list-title"]');
    this.listCopy = this.host.querySelector('[data-role="list-copy"]');
    this.inlineMessage = this.host.querySelector('[data-role="inline-message"]');
    this.toolbarMenuShell = this.host.querySelector('[data-role="toolbar-menu-shell"]');
    this.toolbarMenuToggle = this.host.querySelector('[data-role="toolbar-menu-toggle"]');
    this.toolbarMenuPopover = this.host.querySelector('[data-role="toolbar-menu-popover"]');
    this.utilityBackdrop = this.host.querySelector('[data-role="utility-backdrop"]');
    this.utilityKicker = this.host.querySelector('[data-role="utility-kicker"]');
    this.utilityTitle = this.host.querySelector('[data-role="utility-title"]');
    this.utilityBody = this.host.querySelector('[data-role="utility-body"]');
    this.utilityFooter = this.host.querySelector('[data-role="utility-footer"]');
    this.panelTitle = this.host.querySelector('[data-role="panel-title"]');
    this.panelCopy = this.host.querySelector('[data-role="panel-copy"]');
    this.panelStats = this.host.querySelector('[data-role="panel-stats"]');
    this.panelMeta = this.host.querySelector('[data-role="panel-meta"]');
    this.panelActions = this.host.querySelector('[data-role="panel-actions"]');
    this.periodLabel = this.host.querySelector('[data-role="period-label"]');
    this.periodSubtitle = this.host.querySelector('[data-role="period-subtitle"]');
    this.mobileViewSelect = this.host.querySelector('[data-role="mobile-view-select"]');
    this.liveRegion = this.host.querySelector('[data-role="live-region"]');
    this.loading = this.host.querySelector('[data-role="loading"]');
    this.modalBackdrop = this.host.querySelector('[data-role="modal-backdrop"]');
    this.editorForm = this.host.querySelector('[data-role="editor-form"]');
    this.editorMessage = this.host.querySelector('[data-role="editor-message"]');
    this.editorPlaylists = this.host.querySelector('[data-role="editor-playlists"]');
    this.editorPlaylistSearchShell = this.host.querySelector('[data-role="editor-playlist-search-shell"]');
    this.editorPlaylistSearchInput = this.host.querySelector('[data-role="editor-playlist-search"]');
    this.editorPlaylistSearchNote = this.host.querySelector('[data-role="editor-playlist-search-note"]');
    this.editorPlaylistResults = this.host.querySelector('[data-role="editor-playlist-results"]');
    this.playlistChoiceBackdrop = this.host.querySelector('[data-role="playlist-choice-backdrop"]');
    this.playlistChoiceTitle = this.host.querySelector('[data-role="playlist-choice-title"]');
    this.playlistChoiceCopy = this.host.querySelector('[data-role="playlist-choice-copy"]');
    this.scopeSwitcher = this.host.querySelector('[data-role="scope-switcher"]');
    this.editorFields = {
      eventId: this.host.querySelector('[data-role="editor-event-id"]'),
      title: this.host.querySelector('[data-role="editor-title-input"]'),
      category: this.host.querySelector('[data-role="editor-category"]'),
      type: this.host.querySelector('[data-role="editor-type"]'),
      status: this.host.querySelector('[data-role="editor-status"]'),
      start: this.host.querySelector('[data-role="editor-start"]'),
      end: this.host.querySelector('[data-role="editor-end"]'),
      timezone: this.host.querySelector('[data-role="editor-timezone"]'),
      color: this.host.querySelector('[data-role="editor-color"]'),
      allDay: this.host.querySelector('[data-role="editor-all-day"]'),
      readOnly: this.host.querySelector('[data-role="editor-read-only"]'),
      location: this.host.querySelector('[data-role="editor-location"]'),
      address: this.host.querySelector('[data-role="editor-address"]'),
      customerName: this.host.querySelector('[data-role="editor-customer-name"]'),
      customerEmail: this.host.querySelector('[data-role="editor-customer-email"]'),
      customerPhone: this.host.querySelector('[data-role="editor-customer-phone"]'),
      priceAmount: this.host.querySelector('[data-role="editor-price"]'),
      currency: this.host.querySelector('[data-role="editor-currency"]'),
      description: this.host.querySelector('[data-role="editor-description"]'),
      notes: this.host.querySelector('[data-role="editor-notes"]'),
      logistics: this.host.querySelector('[data-role="editor-logistics"]')
    };
  };

  CalendarController.prototype.bindEvents = function() {
    this.canvas.addEventListener('pointerdown', this.handleCanvasPointerDown);
    this.canvas.addEventListener('pointermove', this.handleCanvasPointerMove);
    this.canvas.addEventListener('pointerleave', this.handleCanvasLeave);
    this.canvas.addEventListener('dblclick', this.handleCanvasDblClick);
    this.canvas.addEventListener('keydown', this.handleCanvasKeyDown);
    window.addEventListener('pointerdown', this.handleWindowPointerDown);
    window.addEventListener('pointermove', this.handleWindowPointerMove);
    window.addEventListener('pointerup', this.handleWindowPointerUp);
    this.toolbar.addEventListener('click', this.handleToolbarClick);
    this.toolbar.addEventListener('change', this.handleToolbarChange);
    this.scopeSwitcher.addEventListener('click', this.handleToolbarClick);
    this.listShell.addEventListener('click', this.handlePanelClick);
    this.host.querySelector('[data-role="panel"]').addEventListener('click', this.handlePanelClick);
    this.editorForm.addEventListener('submit', this.handleModalSubmit);
    this.modalBackdrop.addEventListener('click', this.handleModalClick);
    this.editorForm.addEventListener('change', this.handleModalChange);
    this.editorForm.addEventListener('input', this.handleModalInput);
    this.utilityBackdrop.addEventListener('click', this.handleUtilityClick);
  };

  CalendarController.prototype.unbindEvents = function() {
    this.canvas.removeEventListener('pointerdown', this.handleCanvasPointerDown);
    this.canvas.removeEventListener('pointermove', this.handleCanvasPointerMove);
    this.canvas.removeEventListener('pointerleave', this.handleCanvasLeave);
    this.canvas.removeEventListener('dblclick', this.handleCanvasDblClick);
    this.canvas.removeEventListener('keydown', this.handleCanvasKeyDown);
    window.removeEventListener('pointerdown', this.handleWindowPointerDown);
    window.removeEventListener('pointermove', this.handleWindowPointerMove);
    window.removeEventListener('pointerup', this.handleWindowPointerUp);
    this.toolbar.removeEventListener('click', this.handleToolbarClick);
    this.toolbar.removeEventListener('change', this.handleToolbarChange);
    this.scopeSwitcher.removeEventListener('click', this.handleToolbarClick);
    this.listShell.removeEventListener('click', this.handlePanelClick);
    this.host.querySelector('[data-role="panel"]').removeEventListener('click', this.handlePanelClick);
    this.editorForm.removeEventListener('submit', this.handleModalSubmit);
    this.modalBackdrop.removeEventListener('click', this.handleModalClick);
    this.editorForm.removeEventListener('change', this.handleModalChange);
    this.editorForm.removeEventListener('input', this.handleModalInput);
    this.utilityBackdrop.removeEventListener('click', this.handleUtilityClick);
  };

  CalendarController.prototype.scheduleRender = function() {
    if (this.frameHandle) {
      return;
    }

    var self = this;
    this.frameHandle = window.requestAnimationFrame(function() {
      self.frameHandle = 0;
      self.renderBound();
    });
  };

  CalendarController.prototype.refreshUi = function() {
    var activeView = this.state.view;
    this.host.querySelectorAll('[data-view]').forEach(function(button) {
      button.classList.toggle('is-active', button.getAttribute('data-view') === activeView);
    });
    if (this.mobileViewSelect) {
      this.mobileViewSelect.value = activeView;
    }
    this.host.querySelectorAll('[data-scope]').forEach(function(button) {
      button.classList.toggle('is-active', button.getAttribute('data-scope') === this.state.listScope);
    }, this);
    this.periodLabel.textContent = formatPeriodLabel(activeView === 'list' ? this.state.listScope : activeView, this.state.anchorDateKey, this.options.weekStartsOn);
    this.periodSubtitle.textContent = 'Rendered in ' + this.state.timezone + ' | ' + this.state.visibleEvents.length + ' visible event' + (this.state.visibleEvents.length === 1 ? '' : 's');
    this.listShell.classList.toggle('is-visible', activeView === 'list');
    this.canvasShell.style.display = activeView === 'list' ? 'none' : 'block';
    this.loading.classList.toggle('is-visible', !!this.state.busy);
    this.renderInlineMessage();
    this.renderStatusChips();
    this.renderPanel();
    this.renderList();
  };

  CalendarController.prototype.renderInlineMessage = function() {
    var message = asText(this.state.message);
    this.inlineMessage.textContent = message;
    this.statusbar.classList.toggle('is-visible', message !== '');
    this.inlineMessage.classList.toggle('is-visible', message !== '');
    this.inlineMessage.classList.toggle('is-error', this.state.messageTone === 'error');
    this.inlineMessage.classList.toggle('is-success', this.state.messageTone === 'success');
  };

  CalendarController.prototype.setMessage = function(message, tone) {
    this.state.message = asText(message);
    this.state.messageTone = asText(tone) || 'info';
    this.renderInlineMessage();
  };

  CalendarController.prototype.renderStatusChips = function() {
    return;
  };

  CalendarController.prototype.getCurrentRange = function() {
    return scopeRange(this.state.view === 'list' ? this.state.listScope : this.state.view, this.state.anchorDateKey, this.options.weekStartsOn);
  };

  CalendarController.prototype.getVisibleEvents = function(scope) {
    var effectiveScope = asText(scope) || (this.state.view === 'list' ? this.state.listScope : this.state.view);
    var range = scopeRange(effectiveScope, this.state.anchorDateKey, this.options.weekStartsOn);
    return this.state.events.filter(function(event) {
      return eventIntersectsRange(event, range.startKey, range.endKey, this.state.timezone, this.state.locale);
    }, this).sort(compareEvents);
  };

  CalendarController.prototype.setEvents = function(events) {
    this.state.events = safeArray(events).map(function(event) {
      return normalizeEvent(event, this.state.timezone);
    }, this).sort(compareEvents);
    this.state.visibleEvents = this.getVisibleEvents();
    this.state.selectedEvent = this.getSelectedEvent();
    this.refreshUi();
    this.scheduleRender();
  };

  CalendarController.prototype.updateOptions = function(options) {
    var settings = safeObject(options);
    this.options = Object.assign({}, this.options, settings);
    if (settings.selectedDate) {
      this.state.anchorDateKey = ensureDateKey(settings.selectedDate);
      this.state.selectedDateKey = ensureDateKey(settings.selectedDate);
      this.state.focusedDateKey = ensureDateKey(settings.selectedDate);
    }
    if (settings.timezone) {
      this.state.timezone = asText(settings.timezone) || this.state.timezone;
    }
    if (settings.locale) {
      this.state.locale = asText(settings.locale) || this.state.locale;
    }
    if (settings.initialView) {
      this.setView(settings.initialView, false);
    }
    if (settings.events) {
      this.setEvents(settings.events);
      return;
    }
    this.state.visibleEvents = this.getVisibleEvents();
    this.refreshUi();
    this.scheduleRender();
  };

  CalendarController.prototype.destroy = function() {
    if (this.frameHandle) {
      window.cancelAnimationFrame(this.frameHandle);
      this.frameHandle = 0;
    }
    if (this.editorPlaylistSearchTimer) {
      window.clearTimeout(this.editorPlaylistSearchTimer);
      this.editorPlaylistSearchTimer = 0;
    }
    this.unbindEvents();
    if (this.surface) {
      this.surface.destroy();
    }
    this.host.innerHTML = '';
  };

  CalendarController.prototype.getSelectedEvent = function() {
    if (this.state.selectedEventId === '') {
      return null;
    }
    for (var index = 0; index < this.state.events.length; index += 1) {
      if (this.state.events[index].id === this.state.selectedEventId || this.state.events[index].eventId === this.state.selectedEventId) {
        return this.state.events[index];
      }
    }
    return null;
  };

  CalendarController.prototype.selectEventById = function(eventId, announce) {
    var safeId = asText(eventId);
    this.state.selectedEventId = safeId;
    this.state.selectedEvent = this.getSelectedEvent();
    if (this.state.selectedEvent) {
      this.state.selectedDateKey = getDateKeyFromIso(this.state.selectedEvent.startUtc, this.state.timezone, this.state.locale);
      this.state.focusedDateKey = this.state.selectedDateKey;
    }
    this.renderPanel();
    this.renderStatusChips();
    if (announce !== false) {
      this.announceSelection();
    }
    if (typeof this.options.onSelectionChange === 'function') {
      this.options.onSelectionChange(this.state.selectedEvent, {
        selectedDate: this.state.selectedDateKey,
        view: this.state.view
      });
    }
    this.scheduleRender();
  };

  CalendarController.prototype.selectDate = function(dateKey, updateAnchor) {
    var safeDateKey = ensureDateKey(dateKey);
    this.state.selectedDateKey = safeDateKey;
    this.state.focusedDateKey = safeDateKey;
    if (updateAnchor !== false) {
      this.state.anchorDateKey = safeDateKey;
      this.state.visibleEvents = this.getVisibleEvents();
      this.refreshUi();
      if (typeof this.options.onDateChange === 'function') {
        this.options.onDateChange(safeDateKey, {
          view: this.state.view,
          range: this.getCurrentRange()
        });
      }
    } else {
      this.renderPanel();
      this.renderStatusChips();
    }
    this.announceSelection();
    this.scheduleRender();
  };

  CalendarController.prototype.setToolbarMenuOpen = function(isOpen) {
    this.toolbarMenuOpen = !!isOpen;
    if (this.toolbarMenuShell) {
      this.toolbarMenuShell.classList.toggle('is-open', this.toolbarMenuOpen);
    }
    if (this.toolbarMenuToggle) {
      this.toolbarMenuToggle.setAttribute('aria-expanded', this.toolbarMenuOpen ? 'true' : 'false');
    }
    if (this.toolbarMenuOpen) {
      this.positionToolbarMenu();
    }
  };

  CalendarController.prototype.positionToolbarMenu = function() {
    if (!this.toolbarMenuOpen || !this.toolbarMenuPopover || !this.toolbarMenuToggle) {
      return;
    }

    var triggerRect = this.toolbarMenuToggle.getBoundingClientRect();
    var menuWidth = this.toolbarMenuPopover.offsetWidth || 152;
    var menuHeight = this.toolbarMenuPopover.offsetHeight || 96;
    var left = Math.min(Math.max(12, triggerRect.right - menuWidth), Math.max(12, window.innerWidth - menuWidth - 12));
    var top = triggerRect.bottom + 8;
    if (top + menuHeight > window.innerHeight - 12) {
      top = Math.max(12, triggerRect.top - menuHeight - 8);
    }
    this.toolbarMenuPopover.style.left = Math.round(left) + 'px';
    this.toolbarMenuPopover.style.top = Math.round(top) + 'px';
  };

  CalendarController.prototype.renderUtilityModal = function() {
    if (!this.utilityBackdrop || !this.utilityTitle || !this.utilityBody || !this.utilityFooter || !this.utilityKicker) {
      return;
    }

    var kind = asText(this.utilityModalKind);
    var isOpen = kind !== '';
    this.utilityBackdrop.classList.toggle('is-open', isOpen);
    this.utilityBackdrop.setAttribute('aria-hidden', isOpen ? 'false' : 'true');

    if (!isOpen) {
      this.utilityKicker.textContent = '';
      this.utilityTitle.textContent = '';
      this.utilityBody.innerHTML = '';
      this.utilityFooter.innerHTML = '';
      this.utilityFooter.style.display = 'none';
      return;
    }

    if (kind === 'settings') {
      this.utilityKicker.textContent = 'Calendar settings';
      this.utilityTitle.textContent = 'Display preferences';
      this.utilityBody.innerHTML = ''
        + '<p>Choose the timezone used to render the canvas, side panel, and exports for the current workspace.</p>'
        + '<label class="zy-calendar-editor-field">'
        + '<span class="zy-calendar-editor-label">Display timezone</span>'
        + '<input class="zy-calendar-editor-input" data-role="utility-timezone-input" list="zy-calendar-timezones" value="' + escapeHtml(this.state.timezone) + '" aria-label="Display timezone" />'
        + '</label>'
        + '<p>Event times stay stored in UTC. This setting only changes how the current page is displayed.</p>';
      this.utilityFooter.innerHTML = ''
        + '<button type="button" class="zy-calendar-button" data-action="close-utility">Cancel</button>'
        + '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="apply-utility-settings">Apply timezone</button>';
      this.utilityFooter.style.display = 'flex';
      return;
    }

    this.utilityKicker.textContent = 'Quick help';
    this.utilityTitle.textContent = 'Using the canvas';
    var editingHelp = this.options.allowCreate || this.options.allowEdit
      ? '<p>The calendar is optimized for fast editing directly on the canvas.</p>'
      : '<p>This calendar is in read-only projection mode for scheduled runs.</p>';
    var createHelp = this.options.allowCreate
      ? '<li>Double click empty space to create a timed event for that date.</li>'
      : '';
    var moveHelp = this.options.allowEdit && this.options.allowDragDrop
      ? '<li>Drag timed blocks in day or week view to move them.</li>'
      : '';
    var resizeHelp = this.options.allowEdit && this.options.allowResize
      ? '<li>Resize event edges to change duration.</li>'
      : '';
    this.utilityBody.innerHTML = ''
      + editingHelp
      + '<ul class="zy-calendar-utility-list">'
      + createHelp
      + moveHelp
      + resizeHelp
      + '<li>Switch to list view when you want a compact overview or exports for the visible range.</li>'
      + '</ul>';
    this.utilityFooter.innerHTML = '';
    this.utilityFooter.style.display = 'none';
  };

  CalendarController.prototype.openUtilityModal = function(kind) {
    this.utilityModalKind = asText(kind);
    this.setToolbarMenuOpen(false);
    this.renderUtilityModal();
    window.requestAnimationFrame(function() {
      if (!this.utilityBackdrop) {
        return;
      }
      var preferredTarget = this.utilityBackdrop.querySelector('[data-role="utility-timezone-input"]')
        || this.utilityBackdrop.querySelector('[data-action="close-utility"]');
      if (preferredTarget && typeof preferredTarget.focus === 'function') {
        preferredTarget.focus();
      }
    }.bind(this));
  };

  CalendarController.prototype.closeUtilityModal = function() {
    if (this.utilityModalKind === '') {
      return;
    }
    this.utilityModalKind = '';
    this.renderUtilityModal();
  };

  CalendarController.prototype.applyTimezone = function(nextTimezone) {
    var safeTimezone = asText(nextTimezone) || 'UTC';
    this.state.timezone = safeTimezone;
    this.state.visibleEvents = this.getVisibleEvents();
    this.refreshUi();
    this.scheduleRender();
    if (typeof this.options.onTimezoneChange === 'function') {
      this.options.onTimezoneChange(safeTimezone, {
        selectedDate: this.state.selectedDateKey
      });
    }
  };

  CalendarController.prototype.setView = function(view, announce) {
    var safeView = asText(view) || 'week';
    var previousView = this.state.view;
    this.state.view = safeView;
    if (safeView !== 'list') {
      this.state.lastSpatialView = safeView;
      this.state.listScope = safeView;
    }
    this.state.visibleEvents = this.getVisibleEvents();
    this.refreshUi();
    if (previousView === 'list' && safeView !== 'list' && this.surface) {
      this.surface.measure();
    }
    if (announce !== false && typeof this.options.onViewChange === 'function') {
      this.options.onViewChange(safeView, {
        selectedDate: this.state.anchorDateKey
      });
    }
    this.scheduleRender();
  };

  CalendarController.prototype.shiftRange = function(direction) {
    var delta = direction < 0 ? -1 : 1;
    var activeView = this.state.view === 'list' ? this.state.listScope : this.state.view;
    if (activeView === 'day') {
      this.selectDate(DateMath.addDateDays(this.state.anchorDateKey, delta), true);
      return;
    }
    if (activeView === 'week') {
      this.selectDate(DateMath.addDateDays(this.state.anchorDateKey, delta * 7), true);
      return;
    }
    if (activeView === 'month') {
      this.selectDate(DateMath.addDateMonths(this.state.anchorDateKey, delta), true);
      return;
    }
    if (activeView === 'year') {
      var parts = DateMath.parseDateKey(this.state.anchorDateKey) || { year: new Date().getUTCFullYear(), month: 1, day: 1 };
      this.selectDate((parts.year + delta) + '-' + padNumber(parts.month) + '-' + padNumber(parts.day), true);
      return;
    }
    this.selectDate(DateMath.addDateDays(this.state.anchorDateKey, delta), true);
  };

  CalendarController.prototype.announceSelection = function() {
    var selectedEvent = this.getSelectedEvent();
    if (selectedEvent) {
      this.liveRegion.textContent = selectedEvent.title + '. ' + formatRangeLabel(selectedEvent.startUtc, selectedEvent.endUtc, selectedEvent.allDay, this.state.timezone, this.state.locale);
      return;
    }
    this.liveRegion.textContent = 'Selected date ' + this.state.selectedDateKey + '.';
  };

  CalendarController.prototype.renderPanel = function() {
    var selectedEvent = this.getSelectedEvent();
    if (selectedEvent) {
      var primaryPlaylist = safeArray(selectedEvent.linkedPlaylists).find(function(playlist) {
        return !!safeObject(playlist).isPrimaryEvent;
      }) || safeArray(selectedEvent.linkedPlaylists)[0] || null;
      var connectedPlaylistUrl = primaryPlaylist ? asText(safeObject(primaryPlaylist).builderUrl) : '';
      this.panelTitle.textContent = selectedEvent.title;
      this.panelCopy.textContent = formatRangeLabel(selectedEvent.startUtc, selectedEvent.endUtc, selectedEvent.allDay, this.state.timezone, this.state.locale);
      this.panelStats.innerHTML = ''
        + this.renderStat('Status', selectedEvent.status)
        + this.renderStat('Type', selectedEvent.eventType)
        + this.renderStat('Playlists', String(selectedEvent.linkedPlaylistCount || 0))
        + this.renderStat('Checklist', String(selectedEvent.checklistItemCount || 0));
      this.panelMeta.innerHTML = ''
        + this.renderMeta('Timezone', selectedEvent.timezone)
        + this.renderMeta('Location', selectedEvent.locationLabel || 'Not set')
        + this.renderMeta('Customer', selectedEvent.customerName || 'Not set')
        + this.renderMeta('Category', selectedEvent.category || 'Event')
        + this.renderMeta('Description', selectedEvent.description || 'No description')
        + this.renderMeta('Notes', selectedEvent.notes || 'No notes');
      var canEditSelected = this.options.allowEdit && !selectedEvent.readOnly;
      this.panelActions.innerHTML = ''
        + (canEditSelected ? '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="edit-selected">Edit selected</button>' : '')
        + '<button type="button" class="zy-calendar-button" data-action="focus-selected">Focus on event</button>'
        + (connectedPlaylistUrl !== '' ? ('<a class="zy-calendar-button" href="' + escapeHtml(connectedPlaylistUrl) + '" target="_blank" rel="noopener">Connected Playlist</a>') : '')
        + (selectedEvent.playlistsBuilderUrl !== '' ? ('<a class="zy-calendar-button" href="' + escapeHtml(selectedEvent.playlistsBuilderUrl) + '">Playlist builder</a>') : '')
        + (this.options.allowDelete && !selectedEvent.readOnly ? '<button type="button" class="zy-calendar-button zy-calendar-button-danger" data-action="delete-selected">Delete</button>' : '');
      return;
    }

    var range = this.getCurrentRange();
    this.panelTitle.textContent = 'Visible range';
    this.panelCopy.textContent = range.startKey + ' to ' + range.endKey + ' in ' + this.state.timezone + '.';
    this.panelStats.innerHTML = ''
      + this.renderStat('Visible', String(this.state.visibleEvents.length))
      + this.renderStat('All day', String(this.state.visibleEvents.filter(function(event) { return event.allDay; }).length))
      + this.renderStat('Timed', String(this.state.visibleEvents.filter(function(event) { return !event.allDay; }).length))
      + this.renderStat('Selected', this.state.selectedDateKey);
    var keyboardText = this.options.allowEdit
      ? 'Arrows move, Enter edits, Delete removes'
      : 'Arrows move, Enter selects';
    var modeText = this.options.allowCreate
      ? 'Double click empty space or use Add event'
      : 'Read-only projection';
    this.panelMeta.innerHTML = ''
      + this.renderMeta('Current view', this.state.view === 'list' ? ('List / ' + this.state.listScope) : this.state.view)
      + this.renderMeta('Anchor date', this.state.anchorDateKey)
      + this.renderMeta('Display timezone', this.state.timezone)
      + this.renderMeta('Locale', this.state.locale)
      + this.renderMeta('Keyboard', keyboardText)
      + this.renderMeta(this.options.allowCreate ? 'Create' : 'Mode', modeText);
    this.panelActions.innerHTML = ''
      + (this.options.allowCreate ? '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="add-event">Add event</button>' : '')
      + '<button type="button" class="zy-calendar-button" data-action="go-list">Open list</button>';
  };

  CalendarController.prototype.renderStat = function(label, value) {
    return '<div class="zy-calendar-stat"><span class="zy-calendar-stat-label">' + escapeHtml(label) + '</span><span class="zy-calendar-stat-value">' + escapeHtml(value) + '</span></div>';
  };

  CalendarController.prototype.renderMeta = function(label, value) {
    return '<div class="zy-calendar-event-meta-item"><span class="zy-calendar-event-meta-label">' + escapeHtml(label) + '</span><span class="zy-calendar-event-meta-value">' + escapeHtml(value) + '</span></div>';
  };

  CalendarController.prototype.renderList = function() {
    if (this.state.view !== 'list') {
      return;
    }

    var events = this.getVisibleEvents(this.state.listScope);
    this.state.visibleEvents = events;
    this.listTitle.textContent = formatPeriodLabel(this.state.listScope, this.state.anchorDateKey, this.options.weekStartsOn) + ' list';
    this.listCopy.textContent = 'Sorted by start time in ' + this.state.timezone + '. Export uses the visible rows only.';
    if (events.length === 0) {
      this.listContent.innerHTML = '<div class="zy-calendar-empty-state">' + escapeHtml(this.options.emptyMessage) + '</div>';
      return;
    }

    var rows = events.map(function(event) {
      var eventId = asText(event.id || event.eventId);
      var canEdit = this.options.allowEdit && !event.readOnly;
      return '<tr>'
        + '<td class="zy-calendar-list-col-actions"><div class="zy-calendar-list-actions">'
        + renderCalendarListActionButton({
          action: 'select-row',
          eventId: eventId,
          label: 'Select event',
          icon: 'view'
        })
        + (canEdit ? renderCalendarListActionButton({
          action: 'edit-row',
          eventId: eventId,
          label: 'Edit event',
          icon: 'edit',
          primary: true
        }) : '')
        + '</div></td>'
        + '<td><span class="zy-calendar-list-row-title">' + escapeHtml(event.title) + '</span><span class="zy-calendar-list-row-meta">' + escapeHtml(event.eventType + ' | ' + event.status) + '</span></td>'
        + '<td>' + renderListRangeLabel(event.startUtc, event.endUtc, event.allDay, this.state.timezone, this.state.locale) + '</td>'
        + '<td>' + escapeHtml(event.locationLabel || 'Not set') + '</td>'
        + '<td>' + escapeHtml(event.customerName || '') + '</td>'
        + '<td>' + escapeHtml(event.category || '') + '</td>'
        + '</tr>';
    }, this).join('');
    this.listContent.innerHTML = ''
      + '<table class="zy-calendar-list-table">'
      + '<thead><tr><th class="zy-calendar-list-col-actions">Actions</th><th>Event</th><th>Time</th><th>Location</th><th>Customer</th><th>Category</th></tr></thead>'
      + '<tbody>' + rows + '</tbody>'
      + '</table>';
  };

  CalendarController.prototype.supportsPlaylistLinking = function() {
    return typeof this.options.onPlaylistSearch === 'function'
      && typeof this.options.onPlaylistLink === 'function'
      && typeof this.options.onPlaylistClone === 'function'
      && typeof this.options.onPlaylistUnlink === 'function';
  };

  Object.assign(shared, { CalendarController });
})();
