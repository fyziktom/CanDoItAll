(function() {
  if (window.ZyCanvasCalendar) {
    return;
  }
  var shared = window.ZyCanvasCalendarModule;
  if (!shared) { throw new Error('ZyCanvasCalendar foundation must load before 04-render-and-interaction.js.'); }
  var { CanvasSurface, HitRegistry, DateMath, drawMiniMonth, drawTimedGrid, fillRoundedPanel, fitText, wrapText, STYLE_ID, DAY_SHORT, MONTH_SHORT, TIMEZONE_FALLBACKS, injectStyles, asText, asNumber, clamp, safeObject, safeArray, copy, escapeHtml, padNumber, ensureDateKey, normalizeIsoString, minutesToClockLabel, formatterKey, getFormatter, getZonedParts, zonedPartsToDateKey, getDateKeyFromIso, getMinutesFromIso, formatDateKeyLabel, formatDateTimeLabel, formatRangeLabel, formatRangeLabelLines, renderListRangeLabel, renderCalendarActionIcon, renderCalendarToolbarIcon, renderCalendarListActionButton, renderCalendarToolbarIconButton, renderCalendarExportMenuItem, renderCalendarAddEventButton, toLocalInputValue, parseLocalInputValue, zonedLocalToUtcIso, localInputToUtcIso, buildUtcIsoFromDateKeyMinutes, addMinutesToIso, addDaysToIso, durationMinutes, createLocalEventId, normalizeEvent, pluralize, formatConnectionLabel, compareEvents, getEventSpan, compareDateKeys, eventSpansDate, eventIntersectsRange, buildDensityMap, buildTimeZoneList, buildDefaultEvent, formatPeriodLabel, scopeRange, CalendarController } = shared;
  function dayLabel(dateKey) {
    var dayIndex = DateMath.dayOfWeek(dateKey);
    var parsed = DateMath.parseDateKey(dateKey);
    return {
      label: DAY_SHORT[dayIndex],
      subLabel: parsed ? (MONTH_SHORT[parsed.month - 1] + ' ' + parsed.day) : dateKey
    };
  }

  function eventSegmentForDay(event, dateKey, timeZone, locale) {
    var span = getEventSpan(event, timeZone, locale);
    if (compareDateKeys(dateKey, span.startKey) < 0 || compareDateKeys(dateKey, span.endKey) > 0) {
      return null;
    }
    var startMinutes = compareDateKeys(dateKey, span.startKey) === 0 ? getMinutesFromIso(event.startUtc, timeZone, locale) : 0;
    var endMinutes = compareDateKeys(dateKey, span.endKey) === 0 ? getMinutesFromIso(event.endUtc, timeZone, locale) : 1440;
    if (event.allDay) {
      startMinutes = 0;
      endMinutes = 1440;
    } else if (endMinutes <= startMinutes) {
      endMinutes = 1440;
    }

    return {
      event: event,
      dateKey: dateKey,
      startMinutes: startMinutes,
      endMinutes: endMinutes,
      isStart: compareDateKeys(dateKey, span.startKey) === 0,
      isEnd: compareDateKeys(dateKey, span.endKey) === 0
    };
  }

  function layoutOverlapColumns(items) {
    var sorted = items.slice().sort(function(left, right) {
      if (left.startMinutes !== right.startMinutes) {
        return left.startMinutes - right.startMinutes;
      }
      return right.endMinutes - left.endMinutes;
    });
    var clusters = [];
    var cluster = [];
    var clusterEnd = -1;
    sorted.forEach(function(item) {
      if (cluster.length === 0 || item.startMinutes < clusterEnd) {
        cluster.push(item);
        clusterEnd = Math.max(clusterEnd, item.endMinutes);
        return;
      }
      clusters.push(cluster);
      cluster = [item];
      clusterEnd = item.endMinutes;
    });
    if (cluster.length > 0) {
      clusters.push(cluster);
    }

    var result = [];
    clusters.forEach(function(group) {
      var columnEnds = [];
      group.forEach(function(item) {
        var placed = false;
        for (var index = 0; index < columnEnds.length; index += 1) {
          if (item.startMinutes >= columnEnds[index]) {
            item.column = index;
            columnEnds[index] = item.endMinutes;
            placed = true;
            break;
          }
        }
        if (!placed) {
          item.column = columnEnds.length;
          columnEnds.push(item.endMinutes);
        }
      });
      group.forEach(function(item) {
        item.columns = columnEnds.length;
        result.push(item);
      });
    });
    return result;
  }

  function layoutAllDayRows(segments) {
    var rows = [];
    segments.forEach(function(segment) {
      var rowIndex = 0;
      while (true) {
        if (!rows[rowIndex]) {
          rows[rowIndex] = [];
        }
        var collision = rows[rowIndex].some(function(existing) {
          return !(segment.endColumn < existing.startColumn || segment.startColumn > existing.endColumn);
        });
        if (!collision) {
          segment.row = rowIndex;
          rows[rowIndex].push(segment);
          break;
        }
        rowIndex += 1;
      }
    });
    return rows;
  }

  CalendarController.prototype.render = function() {
    this.state.visibleEvents = this.getVisibleEvents(this.state.view === 'list' ? this.state.listScope : this.state.view);
    this.refreshUi();
    if (this.state.view === 'list') {
      this.surface.clear('#ffffff');
      return;
    }

    var ctx = this.surface.context;
    var size = this.surface.size;
    this.registry.clear();
    this.state.layoutCache = {};
    this.surface.clear('#f5f7fb');

    ctx.save();
    ctx.fillStyle = '#eef2ff';
    ctx.fillRect(0, 0, size.width, size.height);
    ctx.restore();

    if (this.state.view === 'week' || this.state.view === 'day') {
      this.renderTimedView(ctx, size, this.state.view);
      return;
    }
    if (this.state.view === 'month') {
      this.renderMonthView(ctx, size);
      return;
    }
    this.renderYearView(ctx, size);
  };

  CalendarController.prototype.renderTimedView = function(ctx, size, mode) {
    var isWeek = mode === 'week';
    var range = scopeRange(mode, this.state.anchorDateKey, this.options.weekStartsOn);
    var dayKeys = [];
    var cursor = range.startKey;
    while (compareDateKeys(cursor, range.endKey) <= 0) {
      dayKeys.push(cursor);
      cursor = DateMath.addDateDays(cursor, 1);
    }

    var outerPad = 18;
    var stageX = outerPad;
    var stageY = outerPad;
    var stageWidth = size.width - (outerPad * 2);
    var stageHeight = size.height - (outerPad * 2);
    var useMiniMonthRail = isWeek && stageWidth >= 920;
    var sideWidth = useMiniMonthRail ? 258 : 0;
    var gap = useMiniMonthRail ? 16 : 0;
    var sidebarX = stageX;
    var mainX = stageX + sideWidth + gap;
    var mainWidth = stageWidth - sideWidth - gap;
    var densityMap = buildDensityMap(this.state.events, this.state.timezone, this.state.locale);
    var selectedDateKey = this.state.selectedDateKey;
    var weekStart = range.startKey;
    var weekEnd = range.endKey;

    if (useMiniMonthRail) {
      fillRoundedPanel(ctx, {
        x: sidebarX,
        y: stageY,
        width: sideWidth,
        height: stageHeight,
        radius: 22,
        fill: 'rgba(255,255,255,.92)',
        stroke: 'rgba(15,23,42,.08)',
        shadowColor: 'rgba(15,23,42,.06)',
        shadowBlur: 16,
        shadowOffsetY: 8
      });
      ctx.save();
      ctx.fillStyle = '#475569';
      ctx.font = '700 12px "Segoe UI",sans-serif';
      ctx.fillText('Mini months', sidebarX + 16, stageY + 24);
      ctx.restore();

      var miniCount = Math.max(2, parseInt(this.options.miniMonthCount || 2, 10) || 2);
      var miniGap = 12;
      var miniHeight = Math.floor((stageHeight - 42 - ((miniCount - 1) * miniGap)) / miniCount);
      var miniBase = DateMath.startOfMonth(weekStart);
      for (var miniIndex = 0; miniIndex < miniCount; miniIndex += 1) {
        var miniDate = DateMath.addDateMonths(miniBase, miniIndex);
        var miniY = stageY + 38 + (miniIndex * (miniHeight + miniGap));
        var mini = drawMiniMonth(ctx, {
          x: sidebarX + 10,
          y: miniY,
          width: sideWidth - 20,
          height: miniHeight,
          dateKey: miniDate,
          weekStartsOn: this.options.weekStartsOn,
          selectedDateKey: selectedDateKey,
          rangeStartKey: weekStart,
          rangeEndKey: weekEnd,
          todayKey: DateMath.todayKey(),
          densityMap: densityMap
        });
        mini.cells.forEach(function(cell) {
          this.registry.add(cell.bounds, {
            type: 'mini-day',
            dateKey: cell.dateKey
          });
        }, this);
      }
    }

    var timedEvents = [];
    var allDaySegments = [];
    var dayEventMap = {};
    dayKeys.forEach(function(dateKey, dayIndex) {
      dayEventMap[dateKey] = [];
      this.state.visibleEvents.forEach(function(event) {
        var segment = eventSegmentForDay(event, dateKey, this.state.timezone, this.state.locale);
        if (!segment) {
          return;
        }
        var span = getEventSpan(event, this.state.timezone, this.state.locale);
        if (event.allDay || compareDateKeys(span.startKey, span.endKey) !== 0) {
          var startColumn = 0;
          var endColumn = dayKeys.length - 1;
          for (var startIndex = 0; startIndex < dayKeys.length; startIndex += 1) {
            if (compareDateKeys(dayKeys[startIndex], span.startKey) >= 0) {
              startColumn = startIndex;
              break;
            }
          }
          for (var endIndex = dayKeys.length - 1; endIndex >= 0; endIndex -= 1) {
            if (compareDateKeys(dayKeys[endIndex], span.endKey) <= 0) {
              endColumn = endIndex;
              break;
            }
          }
          segment.startColumn = startColumn;
          segment.endColumn = endColumn;
          if (dayIndex === startColumn) {
            allDaySegments.push(segment);
          }
          return;
        }
        dayEventMap[dateKey].push(segment);
      }, this);
    }, this);

    var allDayRows = layoutAllDayRows(allDaySegments);
    var allDayHeight = Math.min(118, Math.max(44, 24 + (allDayRows.length * 22)));
    var mainPanelHeight = stageHeight;
    fillRoundedPanel(ctx, {
      x: mainX,
      y: stageY,
      width: mainWidth,
      height: mainPanelHeight,
      radius: 22,
      fill: 'rgba(255,255,255,.94)',
      stroke: 'rgba(15,23,42,.08)',
      shadowColor: 'rgba(15,23,42,.07)',
      shadowBlur: 18,
      shadowOffsetY: 8
    });

    ctx.save();
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(mainX + 1, stageY + 1, mainWidth - 2, allDayHeight);
    ctx.restore();

    var grid = drawTimedGrid(ctx, {
      x: mainX,
      y: stageY + allDayHeight,
      width: mainWidth,
      height: mainPanelHeight - allDayHeight,
      days: dayKeys.map(function(dateKey) {
        return dayLabel(dateKey);
      }).map(function(item, index) {
        return Object.assign({ dateKey: dayKeys[index] }, item);
      }),
      startHour: this.options.businessHoursStart,
      endHour: this.options.businessHoursEnd,
      slotMinutes: this.options.slotMinutes,
      currentDayKey: getDateKeyFromIso(new Date().toISOString(), this.state.timezone, this.state.locale),
      selectedDateKey: this.state.selectedDateKey
    });
    this.state.layoutCache.timed = {
      mode: mode,
      dayKeys: dayKeys,
      dayRects: grid.dayRects,
      timedItems: timedEvents,
      allDayItems: allDaySegments,
      allDayBounds: {
        x: mainX + grid.leftAxisWidth,
        y: stageY + 6,
        width: mainWidth - grid.leftAxisWidth - 8,
        height: allDayHeight - 12
      },
      minuteHeight: grid.minuteHeight,
      bodyY: grid.bodyY,
      dayWidth: grid.dayWidth,
      mainX: mainX,
      stageY: stageY,
      grid: grid
    };

    ctx.save();
    ctx.font = '700 11px "Segoe UI",sans-serif';
    ctx.fillStyle = '#64748b';
    ctx.fillText('All day', mainX + 10, stageY + 18);
    ctx.restore();

    dayKeys.forEach(function(dateKey, index) {
      var rect = grid.dayRects[index];
      if (!rect) {
        return;
      }
      this.registry.add({
        x: rect.x,
        y: stageY,
        width: rect.width,
        height: allDayHeight
      }, {
        type: 'all-day-slot',
        dateKey: dateKey
      });
      this.registry.add(rect, {
        type: 'time-column',
        dateKey: dateKey
      });
      var laidOut = layoutOverlapColumns(dayEventMap[dateKey]);
      laidOut.forEach(function(item) {
        timedEvents.push(item);
        var width = Math.max(26, (rect.width / item.columns) - 8);
        var x = rect.x + (item.column * (rect.width / item.columns)) + 4;
        var topOffsetMinutes = ((item.startMinutes - (this.options.businessHoursStart * 60)));
        var height = Math.max(24, (item.endMinutes - item.startMinutes) * grid.minuteHeight);
        var y = grid.bodyY + (topOffsetMinutes * grid.minuteHeight) + 2;
        item.bounds = {
          x: x,
          y: y,
          width: width,
          height: height
        };
        this.drawTimedEventBlock(ctx, item, selectedDateKey === dateKey && this.state.selectedEventId === item.event.id);
        this.registry.add(item.bounds, {
          type: 'timed-event',
          eventId: item.event.id,
          dateKey: dateKey,
          bounds: item.bounds
        });
        if (this.options.allowEdit && this.options.allowResize && !item.event.readOnly) {
          this.registry.add({
            x: item.bounds.x,
            y: item.bounds.y,
            width: item.bounds.width,
            height: 8
          }, {
            type: 'resize-start',
            eventId: item.event.id,
            dateKey: dateKey,
            bounds: item.bounds
          });
          this.registry.add({
            x: item.bounds.x,
            y: item.bounds.y + item.bounds.height - 8,
            width: item.bounds.width,
            height: 8
          }, {
            type: 'resize-end',
            eventId: item.event.id,
            dateKey: dateKey,
            bounds: item.bounds
          });
        }
      }, this);
    }, this);

    allDaySegments.forEach(function(segment) {
      var startRect = grid.dayRects[segment.startColumn];
      var endRect = grid.dayRects[segment.endColumn];
      if (!startRect || !endRect) {
        return;
      }
      segment.bounds = {
        x: startRect.x + 4,
        y: stageY + 20 + (segment.row * 22),
        width: (endRect.x + endRect.width) - startRect.x - 8,
        height: 18
      };
      this.drawAllDayEventBlock(ctx, segment, this.state.selectedEventId === segment.event.id);
      this.registry.add(segment.bounds, {
        type: 'all-day-event',
        eventId: segment.event.id,
        dateKey: dayKeys[segment.startColumn],
        bounds: segment.bounds
      });
    }, this);

    if (this.state.interaction && this.state.interaction.previewEvent) {
      var previewEvent = this.state.interaction.previewEvent;
      var previewSpan = getEventSpan(previewEvent, this.state.timezone, this.state.locale);
      if (previewEvent.allDay || compareDateKeys(previewSpan.startKey, previewSpan.endKey) !== 0) {
        var previewStart = Math.max(0, dayKeys.findIndex(function(value) { return compareDateKeys(value, previewSpan.startKey) >= 0; }));
        var previewEnd = dayKeys.length - 1;
        for (var previewIndex = dayKeys.length - 1; previewIndex >= 0; previewIndex -= 1) {
          if (compareDateKeys(dayKeys[previewIndex], previewSpan.endKey) <= 0) {
            previewEnd = previewIndex;
            break;
          }
        }
        var previewStartRect = grid.dayRects[previewStart];
        var previewEndRect = grid.dayRects[previewEnd];
        if (previewStartRect && previewEndRect) {
          this.drawAllDayEventBlock(ctx, {
            event: previewEvent,
            bounds: {
              x: previewStartRect.x + 6,
              y: stageY + 20,
              width: (previewEndRect.x + previewEndRect.width) - previewStartRect.x - 12,
              height: 18
            }
          }, true);
        }
      } else {
        var previewDateKey = getDateKeyFromIso(previewEvent.startUtc, this.state.timezone, this.state.locale);
        var previewDayIndex = dayKeys.indexOf(previewDateKey);
        if (previewDayIndex >= 0) {
          var previewRect = grid.dayRects[previewDayIndex];
          var previewMinutes = getMinutesFromIso(previewEvent.startUtc, this.state.timezone, this.state.locale);
          var previewDuration = durationMinutes(previewEvent);
          this.drawTimedEventBlock(ctx, {
            event: previewEvent,
            startMinutes: previewMinutes,
            endMinutes: previewMinutes + previewDuration,
            bounds: {
              x: previewRect.x + 8,
              y: grid.bodyY + ((previewMinutes - (this.options.businessHoursStart * 60)) * grid.minuteHeight) + 2,
              width: previewRect.width - 16,
              height: Math.max(24, previewDuration * grid.minuteHeight)
            }
          }, true);
        }
      }
    }

    var nowIso = new Date().toISOString();
    var nowDateKey = getDateKeyFromIso(nowIso, this.state.timezone, this.state.locale);
    var nowMinutes = getMinutesFromIso(nowIso, this.state.timezone, this.state.locale);
    var todayColumn = dayKeys.indexOf(nowDateKey);
    if (todayColumn >= 0 && nowMinutes >= (this.options.businessHoursStart * 60) && nowMinutes <= (this.options.businessHoursEnd * 60)) {
      var nowRect = grid.dayRects[todayColumn];
      var nowY = grid.bodyY + ((nowMinutes - (this.options.businessHoursStart * 60)) * grid.minuteHeight);
      ctx.save();
      ctx.strokeStyle = '#ef4444';
      ctx.lineWidth = 2;
      ctx.beginPath();
      ctx.moveTo(nowRect.x + 2, nowY);
      ctx.lineTo(nowRect.x + nowRect.width - 2, nowY);
      ctx.stroke();
      ctx.fillStyle = '#ef4444';
      ctx.beginPath();
      ctx.arc(nowRect.x + 8, nowY, 4, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }
  };

  CalendarController.prototype.drawTimedEventBlock = function(ctx, item, isSelected) {
    var event = item.event;
    ctx.save();
    ctx.fillStyle = event.color;
    ctx.globalAlpha = isSelected ? 0.96 : 0.9;
    fillRoundedPanel(ctx, {
      x: item.bounds.x,
      y: item.bounds.y,
      width: item.bounds.width,
      height: item.bounds.height,
      radius: 12,
      fill: event.color,
      stroke: isSelected ? 'rgba(15,23,42,.42)' : 'rgba(255,255,255,.36)',
      lineWidth: isSelected ? 2 : 1
    });
    ctx.restore();

    ctx.save();
    ctx.fillStyle = '#ffffff';
    ctx.font = '700 12px "Segoe UI",sans-serif';
    ctx.textBaseline = 'top';
    var title = fitText(ctx, event.title, item.bounds.width - 12, '...');
    ctx.fillText(title, item.bounds.x + 6, item.bounds.y + 6);
    if (item.bounds.height >= 34) {
      ctx.font = '600 10px "Segoe UI",sans-serif';
      ctx.globalAlpha = 0.88;
      ctx.fillText(minutesToClockLabel(item.startMinutes) + ' - ' + minutesToClockLabel(item.endMinutes), item.bounds.x + 6, item.bounds.y + 21);
    }
    if (item.bounds.height >= 50 && event.locationLabel !== '') {
      ctx.font = '600 10px "Segoe UI",sans-serif';
      ctx.globalAlpha = 0.76;
      ctx.fillText(fitText(ctx, event.locationLabel, item.bounds.width - 12, '...'), item.bounds.x + 6, item.bounds.y + 35);
    }
    ctx.restore();
  };

  CalendarController.prototype.drawAllDayEventBlock = function(ctx, segment, isSelected) {
    var event = segment.event;
    fillRoundedPanel(ctx, {
      x: segment.bounds.x,
      y: segment.bounds.y,
      width: segment.bounds.width,
      height: segment.bounds.height,
      radius: 9,
      fill: event.color,
      stroke: isSelected ? 'rgba(15,23,42,.4)' : 'rgba(255,255,255,.32)',
      lineWidth: isSelected ? 2 : 1
    });
    ctx.save();
    ctx.fillStyle = '#ffffff';
    ctx.font = '700 11px "Segoe UI",sans-serif';
    ctx.textBaseline = 'middle';
    ctx.fillText(fitText(ctx, event.title, segment.bounds.width - 10, '...'), segment.bounds.x + 6, segment.bounds.y + (segment.bounds.height / 2));
    ctx.restore();
  };

  CalendarController.prototype.renderMonthView = function(ctx, size) {
    var pad = 18;
    var bounds = {
      x: pad,
      y: pad,
      width: size.width - (pad * 2),
      height: size.height - (pad * 2)
    };
    fillRoundedPanel(ctx, {
      x: bounds.x,
      y: bounds.y,
      width: bounds.width,
      height: bounds.height,
      radius: 22,
      fill: 'rgba(255,255,255,.94)',
      stroke: 'rgba(15,23,42,.08)',
      shadowColor: 'rgba(15,23,42,.07)',
      shadowBlur: 18,
      shadowOffsetY: 8
    });
    var matrix = DateMath.buildMonthMatrix(this.state.anchorDateKey, this.options.weekStartsOn);
    var headerHeight = 28;
    var cellWidth = bounds.width / 7;
    var cellHeight = (bounds.height - headerHeight) / matrix.length;
    var visibleLimit = 3;
    this.state.layoutCache.month = {
      bounds: bounds,
      cellWidth: cellWidth,
      cellHeight: cellHeight,
      matrix: matrix
    };
    for (var dayIndex = 0; dayIndex < 7; dayIndex += 1) {
      var labelIndex = (this.options.weekStartsOn + dayIndex) % 7;
      ctx.save();
      ctx.fillStyle = '#64748b';
      ctx.font = '700 11px "Segoe UI",sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText(DAY_SHORT[labelIndex], bounds.x + (dayIndex * cellWidth) + (cellWidth / 2), bounds.y + 18);
      ctx.restore();
    }

    matrix.forEach(function(row, rowIndex) {
      row.forEach(function(cell, columnIndex) {
        var x = bounds.x + (columnIndex * cellWidth);
        var y = bounds.y + headerHeight + (rowIndex * cellHeight);
        var isSelected = cell.dateKey === this.state.selectedDateKey;
        var isToday = cell.dateKey === DateMath.todayKey();
        var isPreviewTarget = this.state.interaction && this.state.interaction.targetDateKey === cell.dateKey;
        ctx.save();
        ctx.fillStyle = isPreviewTarget ? 'rgba(16,185,129,.1)' : (isSelected ? 'rgba(79,70,229,.08)' : '#ffffff');
        ctx.fillRect(x + 1, y + 1, cellWidth - 2, cellHeight - 2);
        ctx.strokeStyle = 'rgba(226,232,240,.95)';
        ctx.strokeRect(x, y, cellWidth, cellHeight);
        if (isToday) {
          ctx.strokeStyle = '#0f766e';
          ctx.lineWidth = 2;
          ctx.strokeRect(x + 3, y + 3, cellWidth - 6, cellHeight - 6);
        }
        ctx.fillStyle = cell.inMonth ? '#0f172a' : '#94a3b8';
        ctx.font = '700 12px "Segoe UI",sans-serif';
        ctx.fillText(String(DateMath.parseDateKey(cell.dateKey).day), x + 8, y + 18);
        ctx.restore();
        this.registry.add({
          x: x,
          y: y,
          width: cellWidth,
          height: cellHeight
        }, {
          type: 'month-day',
          dateKey: cell.dateKey
        });

        var items = this.state.visibleEvents.filter(function(event) {
          return eventSpansDate(event, cell.dateKey, this.state.timezone, this.state.locale);
        }, this).sort(function(left, right) {
          if (left.allDay !== right.allDay) {
            return left.allDay ? -1 : 1;
          }
          return compareEvents(left, right);
        });

        items.slice(0, visibleLimit).forEach(function(event, itemIndex) {
          var chipBounds = {
            x: x + 6,
            y: y + 24 + (itemIndex * 18),
            width: cellWidth - 12,
            height: 15
          };
          fillRoundedPanel(ctx, {
            x: chipBounds.x,
            y: chipBounds.y,
            width: chipBounds.width,
            height: chipBounds.height,
            radius: 7,
            fill: event.color,
            stroke: this.state.selectedEventId === event.id ? 'rgba(15,23,42,.4)' : 'rgba(255,255,255,.3)',
            lineWidth: this.state.selectedEventId === event.id ? 2 : 1
          });
          ctx.save();
          ctx.fillStyle = '#ffffff';
          ctx.font = '700 10px "Segoe UI",sans-serif';
          ctx.textBaseline = 'middle';
          ctx.fillText(fitText(ctx, event.title, chipBounds.width - 8, '...'), chipBounds.x + 4, chipBounds.y + 8);
          ctx.restore();
          this.registry.add(chipBounds, {
            type: 'month-event',
            eventId: event.id,
            dateKey: cell.dateKey
          });
        }, this);

        if (items.length > visibleLimit) {
          var moreBounds = {
            x: x + 6,
            y: y + 24 + (visibleLimit * 18),
            width: cellWidth - 12,
            height: 16
          };
          ctx.save();
          ctx.fillStyle = '#475569';
          ctx.font = '700 10px "Segoe UI",sans-serif';
          ctx.fillText('+' + (items.length - visibleLimit) + ' more', moreBounds.x + 2, moreBounds.y + 11);
          ctx.restore();
          this.registry.add(moreBounds, {
            type: 'month-more',
            dateKey: cell.dateKey
          });
        }

        if (this.state.interaction && this.state.interaction.previewEvent && this.state.interaction.targetDateKey === cell.dateKey) {
          fillRoundedPanel(ctx, {
            x: x + 6,
            y: y + cellHeight - 22,
            width: cellWidth - 12,
            height: 15,
            radius: 7,
            fill: this.state.interaction.previewEvent.color,
            stroke: 'rgba(15,23,42,.22)',
            lineWidth: 1
          });
          ctx.save();
          ctx.fillStyle = '#ffffff';
          ctx.font = '700 10px "Segoe UI",sans-serif';
          ctx.textBaseline = 'middle';
          ctx.fillText(fitText(ctx, this.state.interaction.previewEvent.title || 'Preview', cellWidth - 22, '...'), x + 10, y + cellHeight - 14);
          ctx.restore();
        }
      }, this);
    }, this);
  };

  CalendarController.prototype.renderYearView = function(ctx, size) {
    var pad = 18;
    var gap = 14;
    var yearParts = DateMath.parseDateKey(this.state.anchorDateKey) || { year: new Date().getUTCFullYear() };
    var panelWidth = (size.width - (pad * 2) - (gap * 2)) / 3;
    var panelHeight = (size.height - (pad * 2) - (gap * 3)) / 4;
    var densityMap = buildDensityMap(this.state.events, this.state.timezone, this.state.locale);
    this.state.layoutCache.year = {
      panels: []
    };
    for (var monthIndex = 0; monthIndex < 12; monthIndex += 1) {
      var column = monthIndex % 3;
      var row = Math.floor(monthIndex / 3);
      var x = pad + (column * (panelWidth + gap));
      var y = pad + (row * (panelHeight + gap));
      var monthKey = yearParts.year + '-' + padNumber(monthIndex + 1) + '-01';
      var panel = drawMiniMonth(ctx, {
        x: x,
        y: y,
        width: panelWidth,
        height: panelHeight,
        dateKey: monthKey,
        weekStartsOn: this.options.weekStartsOn,
        selectedDateKey: this.state.selectedDateKey,
        todayKey: DateMath.todayKey(),
        densityMap: densityMap
      });
      this.state.layoutCache.year.panels.push({
        dateKey: monthKey,
        bounds: {
          x: x,
          y: y,
          width: panelWidth,
          height: panelHeight
        }
      });
      this.registry.add({
        x: x,
        y: y,
        width: panelWidth,
        height: panelHeight
      }, {
        type: 'year-month',
        dateKey: monthKey
      });
      panel.cells.forEach(function(cell) {
        this.registry.add(cell.bounds, {
          type: 'year-day',
          dateKey: cell.dateKey
        });
      }, this);
    }
  };

  CalendarController.prototype.regionAtEvent = function(event) {
    var point = this.surface.pointFromEvent(event);
    return {
      point: point,
      region: this.registry.find(point.x, point.y)
    };
  };

  CalendarController.prototype.updateCursor = function(region) {
    var cursor = 'default';
    var safeRegion = safeObject(region);
    if (safeRegion.type === 'resize-start' || safeRegion.type === 'resize-end') {
      cursor = 'ns-resize';
    } else if (safeRegion.type === 'timed-event' || safeRegion.type === 'month-event' || safeRegion.type === 'all-day-event') {
      cursor = this.options.allowEdit && this.options.allowDragDrop ? 'grab' : 'pointer';
    } else if (
      safeRegion.type === 'time-column' ||
      safeRegion.type === 'all-day-slot' ||
      safeRegion.type === 'month-day' ||
      safeRegion.type === 'month-more' ||
      safeRegion.type === 'mini-day' ||
      safeRegion.type === 'year-day' ||
      safeRegion.type === 'year-month'
    ) {
      cursor = 'pointer';
    }
    this.canvas.style.cursor = cursor;
  };

  CalendarController.prototype.resolveTimedPoint = function(point) {
    var cache = safeObject(this.state.layoutCache.timed);
    var rects = safeArray(cache.dayRects);
    var foundIndex = -1;
    rects.forEach(function(rect, index) {
      if (foundIndex !== -1) {
        return;
      }
      if (point.x >= rect.x && point.x <= rect.x + rect.width) {
        foundIndex = index;
      }
    });
    if (foundIndex === -1) {
      return null;
    }
    var dateKey = cache.dayKeys[foundIndex];
    var minute = ((point.y - cache.bodyY) / cache.minuteHeight) + (this.options.businessHoursStart * 60);
    var snapped = Math.round(minute / this.options.slotMinutes) * this.options.slotMinutes;
    return {
      dateKey: dateKey,
      minutes: clamp(snapped, this.options.businessHoursStart * 60, (this.options.businessHoursEnd * 60) - this.options.slotMinutes)
    };
  };

  CalendarController.prototype.buildMovedTimedEvent = function(event, dateKey, startMinutes) {
    var safeEvent = normalizeEvent(event, this.state.timezone);
    var duration = durationMinutes(safeEvent);
    var startUtc = buildUtcIsoFromDateKeyMinutes(dateKey, startMinutes, this.state.timezone, this.state.locale);
    return normalizeEvent(Object.assign({}, safeEvent, {
      startUtc: startUtc,
      endUtc: addMinutesToIso(startUtc, duration)
    }), this.state.timezone);
  };

  CalendarController.prototype.buildResizedEvent = function(event, handleType, dateKey, minutes) {
    var safeEvent = normalizeEvent(event, this.state.timezone);
    var targetUtc = buildUtcIsoFromDateKeyMinutes(dateKey, minutes, this.state.timezone, this.state.locale);
    if (handleType === 'resize-start') {
      if (new Date(targetUtc).getTime() >= new Date(safeEvent.endUtc).getTime()) {
        targetUtc = addMinutesToIso(safeEvent.endUtc, -this.options.slotMinutes);
      }
      return normalizeEvent(Object.assign({}, safeEvent, {
        startUtc: targetUtc
      }), this.state.timezone);
    }
    if (new Date(targetUtc).getTime() <= new Date(safeEvent.startUtc).getTime()) {
      targetUtc = addMinutesToIso(safeEvent.startUtc, this.options.slotMinutes);
    }
    return normalizeEvent(Object.assign({}, safeEvent, {
      endUtc: targetUtc
    }), this.state.timezone);
  };

  CalendarController.prototype.buildShiftedDayEvent = function(event, targetDateKey) {
    var safeEvent = normalizeEvent(event, this.state.timezone);
    var span = getEventSpan(safeEvent, this.state.timezone, this.state.locale);
    var diffDays = Math.round((new Date(Date.UTC(DateMath.parseDateKey(targetDateKey).year, DateMath.parseDateKey(targetDateKey).month - 1, DateMath.parseDateKey(targetDateKey).day)).getTime() - new Date(Date.UTC(DateMath.parseDateKey(span.startKey).year, DateMath.parseDateKey(span.startKey).month - 1, DateMath.parseDateKey(span.startKey).day)).getTime()) / 86400000);
    return normalizeEvent(Object.assign({}, safeEvent, {
      startUtc: addDaysToIso(safeEvent.startUtc, diffDays),
      endUtc: addDaysToIso(safeEvent.endUtc, diffDays)
    }), this.state.timezone);
  };

  CalendarController.prototype.activateRegion = function(region) {
    var safeRegion = safeObject(region);
    if (safeRegion.type === 'timed-event' || safeRegion.type === 'month-event' || safeRegion.type === 'all-day-event') {
      this.selectEventById(safeRegion.eventId, true);
      return;
    }
    if (safeRegion.type === 'mini-day' || safeRegion.type === 'year-day' || safeRegion.type === 'month-day' || safeRegion.type === 'all-day-slot') {
      this.selectDate(safeRegion.dateKey, true);
      if (this.state.view === 'year') {
        this.setView('month', true);
      }
      return;
    }
    if (safeRegion.type === 'month-more') {
      this.selectDate(safeRegion.dateKey, true);
      this.state.listScope = 'day';
      this.setView('list', true);
      return;
    }
    if (safeRegion.type === 'year-month') {
      this.selectDate(safeRegion.dateKey, true);
      this.setView('month', true);
    }
  };

  CalendarController.prototype.onCanvasPointerDown = function(event) {
    if (this.state.busy) {
      return;
    }
    this.canvas.focus();
    var resolved = this.regionAtEvent(event);
    var region = resolved.region;
    var point = resolved.point;
    this.state.hoveredRegion = region;
    this.updateCursor(region);
    if (!region) {
      return;
    }

    var selectedEvent = region.eventId ? this.state.events.find(function(item) {
      return item.id === region.eventId || item.eventId === region.eventId;
    }) : null;
    if (selectedEvent) {
      this.selectEventById(selectedEvent.id, false);
    }

    if ((region.type === 'resize-start' || region.type === 'resize-end') && selectedEvent && this.options.allowEdit && this.options.allowResize && !selectedEvent.readOnly) {
      this.state.interaction = {
        type: region.type,
        event: selectedEvent,
        startPoint: point,
        moved: false
      };
      event.preventDefault();
      return;
    }

    if (region.type === 'timed-event' && selectedEvent && this.options.allowEdit && this.options.allowDragDrop && !selectedEvent.readOnly) {
      var startMinutes = getMinutesFromIso(selectedEvent.startUtc, this.state.timezone, this.state.locale);
      var pointerInfo = this.resolveTimedPoint(point);
      this.state.interaction = {
        type: 'move-timed',
        event: selectedEvent,
        startPoint: point,
        offsetMinutes: pointerInfo ? Math.max(0, pointerInfo.minutes - startMinutes) : 0,
        moved: false
      };
      event.preventDefault();
      return;
    }

    if (region.type === 'all-day-event' && selectedEvent && this.options.allowEdit && this.options.allowDragDrop && !selectedEvent.readOnly) {
      this.state.interaction = {
        type: 'move-day-span',
        event: selectedEvent,
        startPoint: point,
        moved: false
      };
      event.preventDefault();
      return;
    }

    if (region.type === 'month-event' && selectedEvent && this.options.allowEdit && this.options.allowDragDrop && !selectedEvent.readOnly) {
      this.state.interaction = {
        type: 'move-month',
        event: selectedEvent,
        startPoint: point,
        moved: false,
        targetDateKey: region.dateKey
      };
      event.preventDefault();
      return;
    }

    if ((region.type === 'time-column' || region.type === 'all-day-slot') && this.options.allowCreate) {
      var timedPoint = this.resolveTimedPoint(point);
      this.state.interaction = {
        type: region.type === 'all-day-slot' ? 'create-day-span' : 'create-timed',
        dateKey: region.dateKey,
        startPoint: point,
        startMinutes: timedPoint ? timedPoint.minutes : 0,
        moved: false
      };
      event.preventDefault();
      return;
    }

    this.activateRegion(region);
  };

  CalendarController.prototype.onCanvasPointerMove = function(event) {
    if (this.state.interaction) {
      return;
    }
    var resolved = this.regionAtEvent(event);
    this.state.hoveredRegion = resolved.region;
    this.updateCursor(resolved.region);
  };

  CalendarController.prototype.onCanvasLeave = function() {
    if (this.state.interaction) {
      return;
    }
    this.state.hoveredRegion = null;
    this.updateCursor(null);
  };

  CalendarController.prototype.onWindowPointerMove = function(event) {
    if (!this.state.interaction) {
      return;
    }

    var interaction = this.state.interaction;
    var point = this.surface.pointFromEvent(event);
    var deltaX = point.x - interaction.startPoint.x;
    var deltaY = point.y - interaction.startPoint.y;
    interaction.moved = interaction.moved || Math.abs(deltaX) > 4 || Math.abs(deltaY) > 4;
    if (!interaction.moved) {
      return;
    }

    if (interaction.type === 'move-timed') {
      var target = this.resolveTimedPoint(point);
      if (!target) {
        return;
      }
      interaction.previewEvent = this.buildMovedTimedEvent(interaction.event, target.dateKey, target.minutes - interaction.offsetMinutes);
      this.selectDate(target.dateKey, false);
      this.scheduleRender();
      return;
    }
    if (interaction.type === 'resize-start' || interaction.type === 'resize-end') {
      var resizeTarget = this.resolveTimedPoint(point);
      if (!resizeTarget) {
        return;
      }
      interaction.previewEvent = this.buildResizedEvent(interaction.event, interaction.type, resizeTarget.dateKey, resizeTarget.minutes);
      this.scheduleRender();
      return;
    }
    if (interaction.type === 'move-day-span' || interaction.type === 'move-month') {
      var region = this.registry.find(point.x, point.y);
      if (region && (region.type === 'month-day' || region.type === 'all-day-slot' || region.type === 'mini-day' || region.type === 'year-day')) {
        interaction.targetDateKey = region.dateKey;
        interaction.previewEvent = this.buildShiftedDayEvent(interaction.event, region.dateKey);
        this.scheduleRender();
      }
      return;
    }
    if (interaction.type === 'create-timed') {
      var createTarget = this.resolveTimedPoint(point);
      if (!createTarget) {
        return;
      }
      var startDateKey = interaction.dateKey;
      var startMinutes = interaction.startMinutes;
      var startStamp = new Date(buildUtcIsoFromDateKeyMinutes(startDateKey, startMinutes, this.state.timezone, this.state.locale)).getTime();
      var endStamp = new Date(buildUtcIsoFromDateKeyMinutes(createTarget.dateKey, createTarget.minutes + this.options.slotMinutes, this.state.timezone, this.state.locale)).getTime();
      var draft = buildDefaultEvent(this.state.timezone, this.state.locale, startDateKey, startMinutes, false);
      draft.endUtc = new Date(Math.max(startStamp + (this.options.slotMinutes * 60000), endStamp)).toISOString();
      if (endStamp < startStamp) {
        draft.startUtc = buildUtcIsoFromDateKeyMinutes(createTarget.dateKey, createTarget.minutes, this.state.timezone, this.state.locale);
        draft.endUtc = new Date(startStamp + (this.options.slotMinutes * 60000)).toISOString();
      }
      interaction.previewEvent = normalizeEvent(draft, this.state.timezone);
      this.scheduleRender();
      return;
    }
    if (interaction.type === 'create-day-span') {
      var createRegion = this.registry.find(point.x, point.y);
      if (createRegion && (createRegion.type === 'all-day-slot' || createRegion.type === 'month-day' || createRegion.type === 'mini-day' || createRegion.type === 'year-day')) {
        interaction.targetDateKey = createRegion.dateKey;
        var draftEvent = buildDefaultEvent(this.state.timezone, this.state.locale, interaction.dateKey, 0, true);
        var startKey = compareDateKeys(interaction.dateKey, createRegion.dateKey) <= 0 ? interaction.dateKey : createRegion.dateKey;
        var endKey = compareDateKeys(interaction.dateKey, createRegion.dateKey) <= 0 ? createRegion.dateKey : interaction.dateKey;
        draftEvent.startUtc = buildUtcIsoFromDateKeyMinutes(startKey, 0, this.state.timezone, this.state.locale);
        draftEvent.endUtc = buildUtcIsoFromDateKeyMinutes(DateMath.addDateDays(endKey, 1), 0, this.state.timezone, this.state.locale);
        interaction.previewEvent = normalizeEvent(draftEvent, this.state.timezone);
        this.scheduleRender();
      }
    }
  };

  CalendarController.prototype.finishInteraction = function(interaction) {
    if (!interaction) {
      return;
    }
    if (!interaction.moved) {
      if (interaction.event) {
        this.selectEventById(interaction.event.id, true);
      } else if (interaction.dateKey) {
        this.selectDate(interaction.dateKey, true);
      }
      return;
    }

    if (interaction.type === 'move-timed' || interaction.type === 'resize-start' || interaction.type === 'resize-end' || interaction.type === 'move-day-span' || interaction.type === 'move-month') {
      if (interaction.previewEvent) {
        this.persistEvent('update', interaction.previewEvent).then(function(result) {
          this.setMessage('Event updated.', 'success');
          this.selectEventById(result.id, true);
        }.bind(this)).catch(function(error) {
          this.setMessage(error && error.message ? error.message : 'Update failed.', 'error');
        }.bind(this));
      }
      return;
    }
    if (interaction.type === 'create-timed' || interaction.type === 'create-day-span') {
      if (!this.options.allowCreate) {
        return;
      }

      if (interaction.previewEvent) {
        this.openEditor(interaction.previewEvent, 'create');
      } else {
        this.openEditor(buildDefaultEvent(this.state.timezone, this.state.locale, interaction.dateKey, interaction.startMinutes || 0, interaction.type === 'create-day-span'), 'create');
      }
    }
  };

  CalendarController.prototype.onCanvasPointerUp = function() {
  };

  CalendarController.prototype.onWindowPointerUp = function() {
    if (!this.state.interaction) {
      return;
    }
    var interaction = this.state.interaction;
    this.state.interaction = null;
    this.finishInteraction(interaction);
    this.scheduleRender();
  };

  CalendarController.prototype.onCanvasDoubleClick = function(event) {
    var resolved = this.regionAtEvent(event);
    var region = resolved.region;
    if (!region) {
      return;
    }
    if (region.eventId) {
      var selectedEvent = this.state.events.find(function(item) {
        return item.id === region.eventId || item.eventId === region.eventId;
      });
      if (selectedEvent && this.options.allowEdit && !selectedEvent.readOnly) {
        this.openEditor(selectedEvent, 'edit');
      }
      return;
    }
    if (!this.options.allowCreate) {
      return;
    }

    if (region.type === 'month-day' || region.type === 'mini-day' || region.type === 'year-day' || region.type === 'all-day-slot') {
      this.openEditor(buildDefaultEvent(this.state.timezone, this.state.locale, region.dateKey, 0, true), 'create');
      return;
    }
    if (region.type === 'time-column') {
      var timedPoint = this.resolveTimedPoint(resolved.point);
      this.openEditor(buildDefaultEvent(this.state.timezone, this.state.locale, region.dateKey, timedPoint ? timedPoint.minutes : 9 * 60, false), 'create');
    }
  };

  CalendarController.prototype.onCanvasKeyDown = function(event) {
    if (event.key === 'ArrowLeft') {
      this.selectDate(DateMath.addDateDays(this.state.selectedDateKey, -1), true);
      event.preventDefault();
      return;
    }
    if (event.key === 'ArrowRight') {
      this.selectDate(DateMath.addDateDays(this.state.selectedDateKey, 1), true);
      event.preventDefault();
      return;
    }
    if (event.key === 'ArrowUp') {
      this.selectDate(DateMath.addDateDays(this.state.selectedDateKey, -7), true);
      event.preventDefault();
      return;
    }
    if (event.key === 'ArrowDown') {
      this.selectDate(DateMath.addDateDays(this.state.selectedDateKey, 7), true);
      event.preventDefault();
      return;
    }
    if (event.key === 'Enter') {
      var selectedEvent = this.getSelectedEvent();
      if (selectedEvent && this.options.allowEdit && !selectedEvent.readOnly) {
        this.openEditor(selectedEvent, 'edit');
      } else if (!selectedEvent && this.options.allowCreate) {
        this.openEditor(buildDefaultEvent(this.state.timezone, this.state.locale, this.state.selectedDateKey, 9 * 60, false), 'create');
      }
      event.preventDefault();
      return;
    }
    if ((event.key === 'Delete' || event.key === 'Backspace') && this.options.allowDelete) {
      var current = this.getSelectedEvent();
      if (current && !current.readOnly && window.confirm('Delete the selected event?')) {
        this.persistDelete(current).then(function() {
          this.setMessage('Event deleted.', 'success');
        }.bind(this)).catch(function(error) {
          this.setMessage(error && error.message ? error.message : 'Delete failed.', 'error');
        }.bind(this));
      }
      event.preventDefault();
      return;
    }
    if (event.key.toLowerCase() === 't') {
      this.selectDate(DateMath.todayKey(), true);
      event.preventDefault();
    }
  };

  window.ZyCanvasCalendar = {
    create: function(options) {
      return new CalendarController(options);
    }
  };
  Object.assign(shared, { dayLabel, eventSegmentForDay, layoutOverlapColumns, layoutAllDayRows });
})();
