(function() {
  'use strict';

  if (window.ZyCanvasPrimitives) {
    return;
  }

  var MONTH_LABELS = ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'];
  var DAY_LABELS = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];

  function asText(value) {
    return String(value || '').trim();
  }

  function asNumber(value, fallback) {
    var parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : fallback;
  }

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function safeObject(value) {
    return value && typeof value === 'object' ? value : {};
  }

  function safeArray(value) {
    return Array.isArray(value) ? value : [];
  }

  function padNumber(value) {
    return String(value).padStart(2, '0');
  }

  function parseDateKey(dateKey) {
    var match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(asText(dateKey));
    if (!match) {
      return null;
    }

    return {
      year: parseInt(match[1], 10),
      month: parseInt(match[2], 10),
      day: parseInt(match[3], 10)
    };
  }

  function formatDateKey(parts) {
    var safe = safeObject(parts);
    return String(safe.year || 0).padStart(4, '0') + '-' + padNumber(safe.month || 1) + '-' + padNumber(safe.day || 1);
  }

  function utcDateFromKey(dateKey) {
    var parsed = parseDateKey(dateKey);
    if (!parsed) {
      return new Date(Date.UTC(2000, 0, 1));
    }

    return new Date(Date.UTC(parsed.year, parsed.month - 1, parsed.day));
  }

  function keyFromUtcDate(date) {
    return formatDateKey({
      year: date.getUTCFullYear(),
      month: date.getUTCMonth() + 1,
      day: date.getUTCDate()
    });
  }

  function addDateDays(dateKey, amount) {
    var date = utcDateFromKey(dateKey);
    date.setUTCDate(date.getUTCDate() + amount);
    return keyFromUtcDate(date);
  }

  function addDateMonths(dateKey, amount) {
    var parsed = parseDateKey(dateKey) || { year: 2000, month: 1, day: 1 };
    var date = new Date(Date.UTC(parsed.year, parsed.month - 1 + amount, 1));
    var maxDay = daysInMonth(date.getUTCFullYear(), date.getUTCMonth() + 1);
    date.setUTCDate(Math.min(parsed.day, maxDay));
    return keyFromUtcDate(date);
  }

  function daysInMonth(year, month) {
    return new Date(Date.UTC(year, month, 0)).getUTCDate();
  }

  function startOfMonth(dateKey) {
    var parsed = parseDateKey(dateKey) || { year: 2000, month: 1, day: 1 };
    return formatDateKey({
      year: parsed.year,
      month: parsed.month,
      day: 1
    });
  }

  function endOfMonth(dateKey) {
    var parsed = parseDateKey(dateKey) || { year: 2000, month: 1, day: 1 };
    return formatDateKey({
      year: parsed.year,
      month: parsed.month,
      day: daysInMonth(parsed.year, parsed.month)
    });
  }

  function compareDateKeys(left, right) {
    return asText(left).localeCompare(asText(right));
  }

  function dayOfWeek(dateKey) {
    return utcDateFromKey(dateKey).getUTCDay();
  }

  function startOfWeek(dateKey, weekStartsOn) {
    var safeWeekStart = clamp(parseInt(weekStartsOn, 10) || 0, 0, 6);
    var currentDay = dayOfWeek(dateKey);
    var offset = (currentDay - safeWeekStart + 7) % 7;
    return addDateDays(dateKey, -offset);
  }

  function endOfWeek(dateKey, weekStartsOn) {
    return addDateDays(startOfWeek(dateKey, weekStartsOn), 6);
  }

  function monthLabel(dateKey) {
    var parsed = parseDateKey(dateKey) || { year: 2000, month: 1, day: 1 };
    return MONTH_LABELS[parsed.month - 1] + ' ' + parsed.year;
  }

  function buildMonthMatrix(dateKey, weekStartsOn) {
    var monthStartKey = startOfMonth(dateKey);
    var monthEndKey = endOfMonth(dateKey);
    var firstVisibleKey = startOfWeek(monthStartKey, weekStartsOn);
    var lastVisibleKey = endOfWeek(monthEndKey, weekStartsOn);
    var rows = [];
    var cursor = firstVisibleKey;

    while (compareDateKeys(cursor, lastVisibleKey) <= 0) {
      var row = [];
      for (var index = 0; index < 7; index += 1) {
        row.push({
          dateKey: cursor,
          inMonth: compareDateKeys(cursor, monthStartKey) >= 0 && compareDateKeys(cursor, monthEndKey) <= 0
        });
        cursor = addDateDays(cursor, 1);
      }
      rows.push(row);
    }

    return rows;
  }

  function buildTodayKey() {
    var now = new Date();
    return formatDateKey({
      year: now.getUTCFullYear(),
      month: now.getUTCMonth() + 1,
      day: now.getUTCDate()
    });
  }

  function roundRectPath(ctx, x, y, width, height, radius) {
    var safeRadius = Math.max(0, Math.min(radius || 0, width / 2, height / 2));
    ctx.beginPath();
    ctx.moveTo(x + safeRadius, y);
    ctx.lineTo(x + width - safeRadius, y);
    ctx.quadraticCurveTo(x + width, y, x + width, y + safeRadius);
    ctx.lineTo(x + width, y + height - safeRadius);
    ctx.quadraticCurveTo(x + width, y + height, x + width - safeRadius, y + height);
    ctx.lineTo(x + safeRadius, y + height);
    ctx.quadraticCurveTo(x, y + height, x, y + height - safeRadius);
    ctx.lineTo(x, y + safeRadius);
    ctx.quadraticCurveTo(x, y, x + safeRadius, y);
    ctx.closePath();
  }

  function fillRoundedPanel(ctx, options) {
    var settings = Object.assign({
      x: 0,
      y: 0,
      width: 0,
      height: 0,
      radius: 18,
      fill: '#ffffff',
      stroke: 'rgba(15, 23, 42, 0.08)',
      lineWidth: 1,
      shadowColor: '',
      shadowBlur: 0,
      shadowOffsetY: 0
    }, safeObject(options));

    ctx.save();
    if (settings.shadowColor) {
      ctx.shadowColor = settings.shadowColor;
      ctx.shadowBlur = settings.shadowBlur;
      ctx.shadowOffsetY = settings.shadowOffsetY;
    }
    roundRectPath(ctx, settings.x, settings.y, settings.width, settings.height, settings.radius);
    ctx.fillStyle = settings.fill;
    ctx.fill();
    if (settings.stroke) {
      ctx.strokeStyle = settings.stroke;
      ctx.lineWidth = settings.lineWidth;
      ctx.stroke();
    }
    ctx.restore();
  }

  function fitText(ctx, text, maxWidth, ellipsis) {
    var measureService = window.CanDoItAll && window.CanDoItAll.textMeasureService;
    if (measureService && typeof measureService.fitText === 'function') {
      return measureService.fitText(ctx, text, maxWidth, ellipsis);
    }

    var value = asText(text);
    var suffix = asText(ellipsis) || '...';
    if (value === '' || ctx.measureText(value).width <= maxWidth) {
      return value;
    }

    var truncated = value;
    while (truncated.length > 0 && ctx.measureText(truncated + suffix).width > maxWidth) {
      truncated = truncated.slice(0, -1);
    }

    return truncated === '' ? suffix : truncated + suffix;
  }

  function wrapText(ctx, text, maxWidth, maxLines) {
    var measureService = window.CanDoItAll && window.CanDoItAll.textMeasureService;
    if (measureService && typeof measureService.wrapText === 'function') {
      return measureService.wrapText(ctx, text, maxWidth, maxLines);
    }

    var value = asText(text);
    if (value === '') {
      return [];
    }

    var tokens = value.replace(/\s+/g, ' ').split(' ');
    var lines = [];
    var current = '';
    tokens.forEach(function(token) {
      var next = current === '' ? token : current + ' ' + token;
      if (current === '' || ctx.measureText(next).width <= maxWidth) {
        current = next;
        return;
      }
      lines.push(current);
      current = token;
    });
    if (current !== '') {
      lines.push(current);
    }

    if (maxLines > 0 && lines.length > maxLines) {
      lines = lines.slice(0, maxLines);
      lines[maxLines - 1] = fitText(ctx, lines[maxLines - 1], maxWidth, '...');
    }

    return lines;
  }

  function HitRegistry() {
    this.items = [];
  }

  HitRegistry.prototype.clear = function() {
    this.items = [];
  };

  HitRegistry.prototype.add = function(bounds, metadata) {
    this.items.push({
      bounds: {
        x: asNumber(bounds.x, 0),
        y: asNumber(bounds.y, 0),
        width: asNumber(bounds.width, 0),
        height: asNumber(bounds.height, 0)
      },
      metadata: safeObject(metadata)
    });
  };

  HitRegistry.prototype.find = function(pointX, pointY) {
    for (var index = this.items.length - 1; index >= 0; index -= 1) {
      var item = this.items[index];
      var bounds = item.bounds;
      if (
        pointX >= bounds.x &&
        pointX <= bounds.x + bounds.width &&
        pointY >= bounds.y &&
        pointY <= bounds.y + bounds.height
      ) {
        return item.metadata;
      }
    }
    return null;
  };

  function CanvasSurface(options) {
    var settings = safeObject(options);
    if (!(settings.canvas instanceof HTMLCanvasElement)) {
      throw new Error('CanvasSurface requires a canvas element.');
    }

    this.canvas = settings.canvas;
    this.context = this.canvas.getContext('2d');
    this.onResize = typeof settings.onResize === 'function' ? settings.onResize : null;
    this.pixelRatio = 1;
    this.size = { width: 0, height: 0 };
    this.resizeTarget = settings.resizeTarget instanceof HTMLElement ? settings.resizeTarget : this.canvas.parentElement;
    this.handleResize = this.measure.bind(this);
    this.resizeObserver = null;

    if (typeof ResizeObserver !== 'undefined' && this.resizeTarget) {
      this.resizeObserver = new ResizeObserver(this.handleResize);
      this.resizeObserver.observe(this.resizeTarget);
    } else {
      window.addEventListener('resize', this.handleResize);
    }

    this.measure();
  }

  CanvasSurface.prototype.measure = function() {
    var canvasRect = this.canvas.getBoundingClientRect();
    var rect = canvasRect;
    if (this.resizeTarget) {
      var targetRect = this.resizeTarget.getBoundingClientRect();
      var targetIsVisible = targetRect.width > 1 && targetRect.height > 1;
      if (targetIsVisible) {
        rect = {
          width: this.resizeTarget.clientWidth || targetRect.width,
          height: this.resizeTarget.clientHeight || targetRect.height
        };
      }
    }

    var width = Math.max(1, Math.round(rect.width || 1));
    var height = Math.max(1, Math.round(rect.height || 1));
    var ratio = Math.max(1, Math.min(3, window.devicePixelRatio || 1));
    this.pixelRatio = ratio;
    this.size.width = width;
    this.size.height = height;
    this.canvas.width = Math.round(width * ratio);
    this.canvas.height = Math.round(height * ratio);
    this.canvas.style.width = width + 'px';
    this.canvas.style.height = height + 'px';
    this.context.setTransform(ratio, 0, 0, ratio, 0, 0);
    this.context.imageSmoothingEnabled = true;
    if (this.onResize) {
      this.onResize(this.size);
    }
  };

  CanvasSurface.prototype.clear = function(fillStyle) {
    this.context.save();
    this.context.setTransform(1, 0, 0, 1, 0, 0);
    this.context.clearRect(0, 0, this.canvas.width, this.canvas.height);
    this.context.restore();
    if (fillStyle) {
      this.context.save();
      this.context.fillStyle = fillStyle;
      this.context.fillRect(0, 0, this.size.width, this.size.height);
      this.context.restore();
    }
  };

  CanvasSurface.prototype.pointFromEvent = function(event) {
    var rect = this.canvas.getBoundingClientRect();
    return {
      x: event.clientX - rect.left,
      y: event.clientY - rect.top
    };
  };

  CanvasSurface.prototype.destroy = function() {
    if (this.resizeObserver) {
      this.resizeObserver.disconnect();
      this.resizeObserver = null;
      return;
    }

    window.removeEventListener('resize', this.handleResize);
  };

  function drawMiniMonth(ctx, options) {
    var settings = Object.assign({
      x: 0,
      y: 0,
      width: 240,
      height: 220,
      dateKey: buildTodayKey(),
      weekStartsOn: 1,
      selectedDateKey: '',
      hoveredDateKey: '',
      rangeStartKey: '',
      rangeEndKey: '',
      todayKey: buildTodayKey(),
      densityMap: {},
      colors: {}
    }, safeObject(options));

    var colors = Object.assign({
      panelFill: 'rgba(255, 255, 255, 0.96)',
      panelStroke: 'rgba(15, 23, 42, 0.08)',
      label: '#334155',
      muted: '#94a3b8',
      selectedFill: '#4f46e5',
      selectedText: '#ffffff',
      rangeFill: 'rgba(79, 70, 229, 0.12)',
      rangeStroke: 'rgba(79, 70, 229, 0.24)',
      todayStroke: '#0f766e',
      offMonthText: '#cbd5e1',
      density: '#10b981'
    }, safeObject(settings.colors));

    var monthStartKey = startOfMonth(settings.dateKey);
    var monthRows = buildMonthMatrix(monthStartKey, settings.weekStartsOn);
    var headerHeight = 30;
    var weekLabelHeight = 18;
    var bodyTop = settings.y + headerHeight + weekLabelHeight + 8;
    var bodyHeight = settings.height - (bodyTop - settings.y) - 10;
    var cellWidth = settings.width / 7;
    var cellHeight = bodyHeight / monthRows.length;
    var cells = [];

    fillRoundedPanel(ctx, {
      x: settings.x,
      y: settings.y,
      width: settings.width,
      height: settings.height,
      radius: 18,
      fill: colors.panelFill,
      stroke: colors.panelStroke,
      lineWidth: 1,
      shadowColor: 'rgba(15, 23, 42, 0.05)',
      shadowBlur: 14,
      shadowOffsetY: 6
    });

    ctx.save();
    ctx.font = '700 13px "DM Sans", sans-serif';
    ctx.fillStyle = colors.label;
    ctx.textBaseline = 'top';
    ctx.fillText(monthLabel(monthStartKey), settings.x + 12, settings.y + 10);
    ctx.restore();

    for (var dayIndex = 0; dayIndex < 7; dayIndex += 1) {
      var labelIndex = (settings.weekStartsOn + dayIndex) % 7;
      ctx.save();
      ctx.font = '700 10px "DM Sans", sans-serif';
      ctx.fillStyle = colors.muted;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'top';
      ctx.fillText(DAY_LABELS[labelIndex].charAt(0), settings.x + (dayIndex * cellWidth) + (cellWidth / 2), settings.y + headerHeight + 2);
      ctx.restore();
    }

    monthRows.forEach(function(row, rowIndex) {
      row.forEach(function(cell, columnIndex) {
        var cellX = settings.x + (columnIndex * cellWidth);
        var cellY = bodyTop + (rowIndex * cellHeight);
        var density = Math.max(0, parseInt(safeObject(settings.densityMap)[cell.dateKey] || 0, 10) || 0);
        var inRange = settings.rangeStartKey !== '' &&
          settings.rangeEndKey !== '' &&
          compareDateKeys(cell.dateKey, settings.rangeStartKey) >= 0 &&
          compareDateKeys(cell.dateKey, settings.rangeEndKey) <= 0;
        var isSelected = cell.dateKey === settings.selectedDateKey;
        var isToday = cell.dateKey === settings.todayKey;

        if (inRange) {
          ctx.save();
          roundRectPath(ctx, cellX + 3, cellY + 2, cellWidth - 6, cellHeight - 4, 9);
          ctx.fillStyle = colors.rangeFill;
          ctx.fill();
          ctx.strokeStyle = colors.rangeStroke;
          ctx.lineWidth = 1;
          ctx.stroke();
          ctx.restore();
        }

        if (isSelected) {
          ctx.save();
          roundRectPath(ctx, cellX + 5, cellY + 4, cellWidth - 10, cellHeight - 8, 10);
          ctx.fillStyle = colors.selectedFill;
          ctx.fill();
          ctx.restore();
        } else if (cell.dateKey === settings.hoveredDateKey) {
          ctx.save();
          roundRectPath(ctx, cellX + 5, cellY + 4, cellWidth - 10, cellHeight - 8, 10);
          ctx.fillStyle = 'rgba(79, 70, 229, 0.08)';
          ctx.fill();
          ctx.restore();
        }

        if (isToday && !isSelected) {
          ctx.save();
          roundRectPath(ctx, cellX + 5, cellY + 4, cellWidth - 10, cellHeight - 8, 10);
          ctx.strokeStyle = colors.todayStroke;
          ctx.lineWidth = 1.5;
          ctx.stroke();
          ctx.restore();
        }

        ctx.save();
        ctx.font = '600 11px "DM Sans", sans-serif';
        ctx.textAlign = 'center';
        ctx.textBaseline = 'middle';
        ctx.fillStyle = isSelected ? colors.selectedText : (cell.inMonth ? colors.label : colors.offMonthText);
        ctx.fillText(String(parseDateKey(cell.dateKey).day), cellX + (cellWidth / 2), cellY + 12);
        ctx.restore();

        if (density > 0) {
          var markerCount = Math.min(3, density);
          var markerY = cellY + cellHeight - 12;
          for (var markerIndex = 0; markerIndex < markerCount; markerIndex += 1) {
            ctx.save();
            ctx.beginPath();
            ctx.arc(cellX + (cellWidth / 2) + ((markerIndex - ((markerCount - 1) / 2)) * 6), markerY, 1.6, 0, Math.PI * 2);
            ctx.fillStyle = isSelected ? 'rgba(255, 255, 255, 0.92)' : colors.density;
            ctx.fill();
            ctx.restore();
          }
        }

        cells.push({
          dateKey: cell.dateKey,
          inMonth: cell.inMonth,
          bounds: {
            x: cellX,
            y: cellY,
            width: cellWidth,
            height: cellHeight
          }
        });
      });
    });

    return {
      monthStartKey: monthStartKey,
      cells: cells
    };
  }

  function drawTimedGrid(ctx, options) {
    var settings = Object.assign({
      x: 0,
      y: 0,
      width: 800,
      height: 600,
      days: [],
      startHour: 0,
      endHour: 24,
      slotMinutes: 30,
      currentDayKey: '',
      selectedDateKey: '',
      colors: {}
    }, safeObject(options));
    var colors = Object.assign({
      panelFill: 'rgba(255, 255, 255, 0.98)',
      panelStroke: 'rgba(15, 23, 42, 0.08)',
      headerFill: 'rgba(248, 250, 252, 0.92)',
      gridMajor: 'rgba(148, 163, 184, 0.32)',
      gridMinor: 'rgba(226, 232, 240, 0.88)',
      axisText: '#64748b',
      headerText: '#334155',
      currentDayFill: 'rgba(79, 70, 229, 0.05)',
      selectedDayFill: 'rgba(16, 185, 129, 0.06)'
    }, safeObject(settings.colors));
    var compactWidth = settings.width < 360;
    var narrowWidth = settings.width < 560;
    var leftAxisWidth = compactWidth ? 36 : (narrowWidth ? 46 : 58);
    var headerHeight = narrowWidth ? 34 : 40;
    var bodyX = settings.x + leftAxisWidth;
    var bodyY = settings.y + headerHeight;
    var bodyWidth = settings.width - leftAxisWidth;
    var bodyHeight = settings.height - headerHeight;
    var dayWidth = bodyWidth / Math.max(1, settings.days.length);
    var totalMinutes = Math.max(30, (settings.endHour - settings.startHour) * 60);
    var minuteHeight = bodyHeight / totalMinutes;
    var dayRects = [];

    fillRoundedPanel(ctx, {
      x: settings.x,
      y: settings.y,
      width: settings.width,
      height: settings.height,
      radius: 20,
      fill: colors.panelFill,
      stroke: colors.panelStroke,
      lineWidth: 1,
      shadowColor: 'rgba(15, 23, 42, 0.06)',
      shadowBlur: 18,
      shadowOffsetY: 8
    });

    ctx.save();
    roundRectPath(ctx, settings.x, settings.y, settings.width, headerHeight, 20);
    ctx.clip();
    ctx.fillStyle = colors.headerFill;
    ctx.fillRect(settings.x, settings.y, settings.width, headerHeight);
    ctx.restore();

    settings.days.forEach(function(day, index) {
      var columnX = bodyX + (index * dayWidth);
      var dateKey = asText(day.dateKey);
      var dateParts = parseDateKey(dateKey);
      var primaryLabel = narrowWidth ? asText(day.label).charAt(0) : asText(day.label);
      var secondaryLabel = narrowWidth
        ? (dateParts ? String(dateParts.day) : '')
        : asText(day.subLabel);
      if (dateKey === settings.currentDayKey) {
        ctx.save();
        ctx.fillStyle = colors.currentDayFill;
        ctx.fillRect(columnX, settings.y, dayWidth, settings.height);
        ctx.restore();
      } else if (dateKey === settings.selectedDateKey) {
        ctx.save();
        ctx.fillStyle = colors.selectedDayFill;
        ctx.fillRect(columnX, settings.y, dayWidth, settings.height);
        ctx.restore();
      }

      ctx.save();
      ctx.font = narrowWidth ? '700 10px "DM Sans", sans-serif' : '700 12px "DM Sans", sans-serif';
      ctx.fillStyle = colors.headerText;
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillText(primaryLabel, columnX + (dayWidth / 2), settings.y + (narrowWidth ? 11 : 13));
      ctx.font = narrowWidth ? '600 9px "DM Sans", sans-serif' : '600 11px "DM Sans", sans-serif';
      ctx.fillStyle = colors.axisText;
      ctx.fillText(secondaryLabel, columnX + (dayWidth / 2), settings.y + (narrowWidth ? 23 : 28));
      ctx.restore();

      ctx.save();
      ctx.strokeStyle = index === 0 ? colors.gridMajor : colors.gridMinor;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(columnX, settings.y);
      ctx.lineTo(columnX, settings.y + settings.height);
      ctx.stroke();
      ctx.restore();

      dayRects.push({
        dateKey: dateKey,
        x: columnX,
        y: bodyY,
        width: dayWidth,
        height: bodyHeight
      });
    });

    ctx.save();
    ctx.strokeStyle = colors.gridMajor;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(settings.x, headerHeight + settings.y);
    ctx.lineTo(settings.x + settings.width, headerHeight + settings.y);
    ctx.stroke();
    ctx.restore();

    var slotMinutes = Math.max(5, Math.min(120, parseInt(settings.slotMinutes, 10) || 30));
    for (var hour = settings.startHour; hour <= settings.endHour; hour += 1) {
      var lineY = bodyY + ((hour - settings.startHour) * 60 * minuteHeight);
      ctx.save();
      ctx.strokeStyle = colors.gridMajor;
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.moveTo(settings.x, lineY);
      ctx.lineTo(settings.x + settings.width, lineY);
      ctx.stroke();
      ctx.restore();

      if (hour < settings.endHour && slotMinutes < 60) {
        for (var minute = slotMinutes; minute < 60; minute += slotMinutes) {
          var minorY = lineY + (minute * minuteHeight);
          ctx.save();
          ctx.strokeStyle = colors.gridMinor;
          ctx.lineWidth = 1;
          ctx.beginPath();
          ctx.moveTo(bodyX, minorY);
          ctx.lineTo(settings.x + settings.width, minorY);
          ctx.stroke();
          ctx.restore();
        }
      }

      if (hour < settings.endHour) {
        var displayHour = hour % 24;
        var suffix = displayHour >= 12 ? 'PM' : 'AM';
        var labelHour = displayHour % 12;
        if (labelHour === 0) {
          labelHour = 12;
        }
        var axisLabel = narrowWidth ? String(labelHour) : (labelHour + ' ' + suffix);
        ctx.save();
        ctx.font = narrowWidth ? '600 9px "DM Sans", sans-serif' : '600 11px "DM Sans", sans-serif';
        ctx.fillStyle = colors.axisText;
        ctx.textAlign = 'right';
        ctx.textBaseline = 'middle';
        ctx.fillText(axisLabel, settings.x + leftAxisWidth - 8, lineY + 2);
        ctx.restore();
      }
    }

    ctx.save();
    ctx.strokeStyle = colors.gridMajor;
    ctx.lineWidth = 1;
    ctx.beginPath();
    ctx.moveTo(settings.x + settings.width, settings.y);
    ctx.lineTo(settings.x + settings.width, settings.y + settings.height);
    ctx.stroke();
    ctx.restore();

    return {
      leftAxisWidth: leftAxisWidth,
      headerHeight: headerHeight,
      bodyX: bodyX,
      bodyY: bodyY,
      bodyWidth: bodyWidth,
      bodyHeight: bodyHeight,
      dayWidth: dayWidth,
      minuteHeight: minuteHeight,
      dayRects: dayRects
    };
  }

  window.ZyCanvasPrimitives = {
    CanvasSurface: CanvasSurface,
    HitRegistry: HitRegistry,
    DateMath: {
      parseDateKey: parseDateKey,
      formatDateKey: formatDateKey,
      addDateDays: addDateDays,
      addDateMonths: addDateMonths,
      compareDateKeys: compareDateKeys,
      dayOfWeek: dayOfWeek,
      startOfWeek: startOfWeek,
      endOfWeek: endOfWeek,
      startOfMonth: startOfMonth,
      endOfMonth: endOfMonth,
      daysInMonth: daysInMonth,
      buildMonthMatrix: buildMonthMatrix,
      monthLabel: monthLabel,
      todayKey: buildTodayKey
    },
    roundRectPath: roundRectPath,
    fillRoundedPanel: fillRoundedPanel,
    fitText: fitText,
    wrapText: wrapText,
    drawMiniMonth: drawMiniMonth,
    drawTimedGrid: drawTimedGrid
  };
})();
