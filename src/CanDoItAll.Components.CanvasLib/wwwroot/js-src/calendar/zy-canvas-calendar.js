(function() {
  'use strict';

  if (window.ZyCanvasCalendar) {
    return;
  }

  var primitives = window.ZyCanvasPrimitives;
  if (!primitives) {
    return;
  }

  var CanvasSurface = primitives.CanvasSurface;
  var HitRegistry = primitives.HitRegistry;
  var DateMath = primitives.DateMath;
  var drawMiniMonth = primitives.drawMiniMonth;
  var drawTimedGrid = primitives.drawTimedGrid;
  var fillRoundedPanel = primitives.fillRoundedPanel;
  var fitText = primitives.fitText;
  var wrapText = primitives.wrapText;
  var STYLE_ID = 'zy-canvas-calendar-styles';
  var DAY_SHORT = ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'];
  var MONTH_SHORT = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
  var TIMEZONE_FALLBACKS = [
    'UTC',
    'Europe/Prague',
    'Europe/Berlin',
    'Europe/London',
    'America/New_York',
    'America/Chicago',
    'America/Denver',
    'America/Los_Angeles',
    'America/Sao_Paulo',
    'Asia/Tokyo',
    'Australia/Sydney'
  ];
  var formatterCache = {};

  function injectStyles() {
    if (document.getElementById(STYLE_ID)) {
      return;
    }

    var style = document.createElement('style');
    style.id = STYLE_ID;
    style.textContent = ''
      + '.zy-calendar-shell{--zy-cal-border:rgba(15,23,42,.09);--zy-cal-border-strong:rgba(79,70,229,.24);--zy-cal-bg:#f5f7fb;--zy-cal-panel:#ffffff;--zy-cal-panel-soft:#f8fafc;--zy-cal-text:#0f172a;--zy-cal-muted:#64748b;--zy-cal-accent:#4f46e5;--zy-cal-accent-soft:rgba(79,70,229,.12);--zy-cal-success:#0f766e;--zy-cal-danger:#dc2626;--zy-cal-shadow:0 18px 40px rgba(15,23,42,.08);font-family:"Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-text);}'
      + '.zy-calendar-shell *{box-sizing:border-box;}'
      + '.zy-calendar-toolbar{display:flex;flex-wrap:nowrap;align-items:center;gap:6px;width:100%;margin:0 0 6px;padding:0;overflow-x:auto;overflow-y:hidden;scrollbar-width:thin;-webkit-overflow-scrolling:touch;}'
      + '.zy-calendar-toolbar-group{display:inline-flex;align-items:center;gap:4px;flex-wrap:nowrap;min-width:0;white-space:nowrap;flex:0 0 auto;}'
      + '.zy-calendar-toolbar-divider{display:inline-flex;align-items:center;justify-content:center;color:rgba(100,116,139,.9);font:700 12px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;user-select:none;}'
      + '.zy-calendar-button,.zy-calendar-view-button{border:1px solid var(--zy-cal-border);background:#fff;color:var(--zy-cal-text);border-radius:12px;padding:9px 14px;font:600 13px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;cursor:pointer;transition:background-color .15s ease,border-color .15s ease,color .15s ease,transform .15s ease;}'
      + '.zy-calendar-button:hover,.zy-calendar-view-button:hover{background:#f8fafc;border-color:rgba(79,70,229,.26);}'
      + '.zy-calendar-button:focus-visible,.zy-calendar-view-button:focus-visible,.zy-calendar-export-button:focus-visible,.zy-calendar-toolbar-input:focus-visible,.zy-calendar-canvas:focus-visible,.zy-calendar-editor-input:focus-visible,.zy-calendar-editor-textarea:focus-visible,.zy-calendar-editor-select:focus-visible{outline:2px solid rgba(79,70,229,.5);outline-offset:2px;}'
      + '.zy-calendar-button-primary{background:var(--zy-cal-accent);color:#fff;border-color:var(--zy-cal-accent);}'
      + '.zy-calendar-button-primary:hover{background:#4338ca;border-color:#4338ca;color:#fff;}'
      + '.zy-calendar-button-danger{color:var(--zy-cal-danger);}'
      + '.zy-calendar-button-danger:hover{background:rgba(220,38,38,.05);border-color:rgba(220,38,38,.2);}'
      + '.zy-calendar-view-switcher,.zy-calendar-scope-switcher{display:flex;align-items:center;gap:6px;padding:4px;border:1px solid var(--zy-cal-border);background:#fff;border-radius:14px;}'
      + '.zy-calendar-mobile-view-field{display:none;align-items:center;flex:0 0 auto;}'
      + '.zy-calendar-mobile-view-select{display:none;min-width:112px;}'
      + '.zy-calendar-view-button.is-active{background:var(--zy-cal-accent);color:#fff;border-color:var(--zy-cal-accent);}'
      + '.zy-calendar-export-button{display:inline-flex;align-items:center;justify-content:center;gap:.42rem;}'
      + '.zy-calendar-export-button .export-trigger-icon{width:.95rem;height:.95rem;flex-shrink:0;}'
      + '.zy-calendar-export-button .export-trigger-label{font:700 11px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;letter-spacing:.04em;text-transform:uppercase;}'
      + '.zy-calendar-toolbar .zy-calendar-button,.zy-calendar-toolbar .zy-calendar-view-button,.zy-calendar-toolbar .zy-calendar-export-button{display:inline-flex;align-items:center;justify-content:center;gap:4px;min-height:26px;padding:0 8px;border-radius:999px;font:700 10px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;box-shadow:none;flex:0 0 auto;}'
      + '.zy-calendar-toolbar .zy-calendar-view-switcher{padding:0;border:none;background:transparent;border-radius:0;gap:4px;}'
      + '.zy-calendar-toolbar-meta{display:inline-flex;align-items:center;gap:0;flex-wrap:nowrap;min-width:0;}'
      + '.zy-calendar-period-label{font:700 13px/1.1 "Segoe UI","Helvetica Neue",Arial,sans-serif;letter-spacing:-.02em;white-space:nowrap;}'
      + '.zy-calendar-period-subtitle{display:none;}'
      + '.zy-calendar-toolbar-input{border:1px solid var(--zy-cal-border);border-radius:999px;padding:0 9px;background:#fff;color:var(--zy-cal-text);font:700 10px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;min-height:26px;height:26px;min-width:96px;max-width:108px;box-shadow:none;}'
      + '.zy-calendar-toolbar-icon{display:inline-flex;align-items:center;justify-content:center;width:12px;height:12px;line-height:1;flex-shrink:0;font:800 12px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;}'
      + '.zy-calendar-toolbar-nav-icon{font-size:14px;}'
      + '.zy-calendar-toolbar-event-plus{display:inline-flex;align-items:center;justify-content:center;width:13px;height:13px;border-radius:999px;background:rgba(255,255,255,.16);font:800 11px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;flex-shrink:0;}'
      + '.zy-calendar-toolbar .zy-calendar-button-primary .zy-calendar-toolbar-event-plus{background:rgba(67,40,121,.14);color:currentColor;}'
      + '.zy-calendar-toolbar .zy-calendar-button-primary,.zy-calendar-toolbar-menu-item{background:linear-gradient(135deg,#f2ebff 0%,#ddd0ff 100%);border-color:rgba(124,58,237,.2);color:#432879;box-shadow:0 10px 24px rgba(124,58,237,.12);}'
      + '.zy-calendar-toolbar .zy-calendar-button-primary:hover,.zy-calendar-toolbar-menu-item:hover{background:linear-gradient(135deg,#ebe1ff 0%,#cfbeff 100%);border-color:rgba(107,70,193,.24);color:#352066;}'
      + '.zy-calendar-toolbar .zy-calendar-button-primary .export-trigger-icon,.zy-calendar-toolbar-menu-item .export-trigger-icon{fill:currentColor;}'
      + '.zy-calendar-toolbar-icon-button{width:26px;min-width:26px;padding:0;}'
      + '.zy-calendar-toolbar-icon-button svg{width:14px;height:14px;stroke:currentColor;fill:none;stroke-width:1.85;stroke-linecap:round;stroke-linejoin:round;pointer-events:none;}'
      + '.zy-calendar-toolbar-menu-shell{position:relative;display:inline-flex;align-items:center;}'
      + '.zy-calendar-toolbar-menu-shell.is-open .zy-calendar-toolbar-menu-popover{display:flex;}'
      + '.zy-calendar-toolbar-menu-popover{position:fixed;top:0;left:0;display:none;flex-direction:column;gap:8px;min-width:152px;padding:12px;border:1px solid rgba(226,232,240,.95);border-radius:18px;background:rgba(255,255,255,.98);box-shadow:0 18px 40px rgba(15,23,42,.18);z-index:60;}'
      + '.zy-calendar-toolbar-menu-item{display:flex;align-items:center;justify-content:flex-start;gap:8px;width:100%;min-height:32px;padding:0 11px;border-radius:14px;border:1px solid transparent;font:700 11px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;cursor:pointer;transition:transform .15s ease,filter .15s ease;}'
      + '.zy-calendar-toolbar-menu-item:focus-visible{outline:2px solid rgba(79,70,229,.5);outline-offset:2px;}'
      + '.zy-calendar-toolbar-menu-item .export-trigger-icon{width:14px;height:14px;flex-shrink:0;}'
      + '.zy-calendar-stage-shell{position:relative;}'
      + '.zy-calendar-utility-backdrop{position:absolute;inset:0;display:none;align-items:flex-start;justify-content:center;padding:42px 16px 18px;background:rgba(245,247,251,.82);backdrop-filter:blur(5px);z-index:4;}'
      + '.zy-calendar-utility-backdrop.is-open{display:flex;}'
      + '.zy-calendar-utility-dialog{width:min(420px,100%);border-radius:22px;border:1px solid rgba(226,232,240,.95);background:#fff;box-shadow:0 26px 60px rgba(15,23,42,.2);overflow:hidden;}'
      + '.zy-calendar-utility-header{display:flex;align-items:flex-start;justify-content:space-between;gap:12px;padding:18px 18px 14px;border-bottom:1px solid rgba(226,232,240,.9);background:#fbfcff;}'
      + '.zy-calendar-utility-title{margin:0;font:700 19px/1.15 "Segoe UI","Helvetica Neue",Arial,sans-serif;letter-spacing:-.02em;}'
      + '.zy-calendar-utility-body{display:flex;flex-direction:column;gap:12px;padding:18px;}'
      + '.zy-calendar-utility-body p{margin:0;color:var(--zy-cal-muted);font:500 13px/1.5 "Segoe UI","Helvetica Neue",Arial,sans-serif;}'
      + '.zy-calendar-utility-list{margin:0;padding-left:18px;display:grid;gap:8px;color:var(--zy-cal-text);font:600 13px/1.4 "Segoe UI","Helvetica Neue",Arial,sans-serif;}'
      + '.zy-calendar-utility-list li{margin:0;}'
      + '.zy-calendar-utility-footer{display:flex;justify-content:flex-end;gap:8px;padding:0 18px 18px;}'
      + '.zy-calendar-body{display:grid;grid-template-columns:minmax(0,1fr) 320px;gap:18px;padding:18px;background:var(--zy-cal-bg);border-radius:24px;}'
      + '.zy-calendar-stage,.zy-calendar-panel{min-width:0;}'
      + '.zy-calendar-stage-shell{display:flex;flex-direction:column;gap:8px;}'
      + '.zy-calendar-statusbar{display:none;margin:0 0 4px;}'
      + '.zy-calendar-statusbar.is-visible{display:block;}'
      + '.zy-calendar-chip-row{display:none;}'
      + '.zy-calendar-chip{display:inline-flex;align-items:center;gap:6px;border-radius:999px;padding:6px 10px;background:#eef2ff;color:#3730a3;font:700 11px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;letter-spacing:.02em;text-transform:uppercase;}'
      + '.zy-calendar-chip-muted{background:#e2e8f0;color:#475569;}'
      + '.zy-calendar-chip-ok{background:rgba(15,118,110,.12);color:#0f766e;}'
      + '.zy-calendar-chip-warn{background:rgba(245,158,11,.16);color:#92400e;}'
      + '.zy-calendar-canvas-shell{position:relative;border:1px solid var(--zy-cal-border);border-radius:22px;background:linear-gradient(180deg,#ffffff 0%,#f8fafc 100%);box-shadow:var(--zy-cal-shadow);overflow:hidden;min-height:680px;}'
      + '.zy-calendar-canvas{display:block;width:100%;height:min(78vh,900px);min-height:680px;background:transparent;cursor:default;}'
      + '.zy-calendar-list-shell{display:none;border:1px solid var(--zy-cal-border);border-radius:22px;background:#fff;box-shadow:var(--zy-cal-shadow);overflow:hidden;}'
      + '.zy-calendar-list-shell.is-visible{display:block;}'
      + '.zy-calendar-list-header{display:flex;flex-wrap:wrap;gap:10px;align-items:center;justify-content:space-between;padding:16px 18px;border-bottom:1px solid var(--zy-cal-border);background:#fbfcff;}'
      + '.zy-calendar-list-table-wrap{overflow:auto;max-height:min(76vh,860px);}'
      + '.zy-calendar-list-table{width:100%;border-collapse:collapse;min-width:920px;}'
      + '.zy-calendar-list-table th,.zy-calendar-list-table td{padding:12px 14px;text-align:left;border-bottom:1px solid rgba(226,232,240,.9);font:500 13px/1.35 "Segoe UI","Helvetica Neue",Arial,sans-serif;vertical-align:top;}'
      + '.zy-calendar-list-table th{position:sticky;top:0;background:#f8fafc;color:#475569;font-weight:700;z-index:1;}'
      + '.zy-calendar-list-row-title{font-weight:700;color:var(--zy-cal-text);display:block;margin-bottom:4px;}'
      + '.zy-calendar-list-row-meta{font-size:12px;color:var(--zy-cal-muted);}'
      + '.zy-calendar-list-col-actions{width:88px;}'
      + '.zy-calendar-list-actions{display:flex;flex-wrap:nowrap;gap:8px;margin-top:0;align-items:flex-start;}'
      + '.zy-calendar-list-icon-button{display:inline-flex;align-items:center;justify-content:center;width:34px;height:34px;padding:0;border-radius:12px;border:1px solid rgba(148,163,184,.26);background:#fff;color:#475569;box-shadow:0 8px 20px rgba(15,23,42,.08);transition:transform .18s ease,box-shadow .18s ease,border-color .18s ease,background-color .18s ease,color .18s ease;}'
      + '.zy-calendar-list-icon-button:hover,.zy-calendar-list-icon-button:focus-visible{transform:translateY(-1px);border-color:rgba(99,102,241,.38);box-shadow:0 12px 24px rgba(99,102,241,.18);outline:none;}'
      + '.zy-calendar-list-icon-button svg{width:16px;height:16px;stroke:currentColor;fill:none;stroke-width:1.85;stroke-linecap:round;stroke-linejoin:round;pointer-events:none;}'
      + '.zy-calendar-list-icon-button-primary{background:linear-gradient(135deg,#6d5efc 0%,#4f46e5 100%);border-color:rgba(79,70,229,.68);color:#fff;box-shadow:0 14px 28px rgba(79,70,229,.24);}'
      + '.zy-calendar-list-row-time{white-space:normal;min-width:150px;}'
      + '.zy-calendar-list-row-time-line{display:block;}'
      + '.zy-calendar-list-row-time-line + .zy-calendar-list-row-time-line{margin-top:2px;color:var(--zy-cal-muted);}'
      + '.zy-calendar-empty-state{padding:54px 28px;text-align:center;color:var(--zy-cal-muted);}'
      + '.zy-calendar-panel{display:flex;flex-direction:column;gap:14px;}'
      + '.zy-calendar-panel-card{border:1px solid var(--zy-cal-border);border-radius:20px;background:var(--zy-cal-panel);box-shadow:0 12px 30px rgba(15,23,42,.05);padding:18px;}'
      + '.zy-calendar-panel-kicker{font:700 11px/1.1 "Segoe UI","Helvetica Neue",Arial,sans-serif;letter-spacing:.06em;text-transform:uppercase;color:var(--zy-cal-muted);margin-bottom:8px;}'
      + '.zy-calendar-panel-title{font:700 18px/1.2 "Segoe UI","Helvetica Neue",Arial,sans-serif;letter-spacing:-.02em;margin:0 0 8px;}'
      + '.zy-calendar-panel-copy{margin:0;color:var(--zy-cal-muted);font:500 13px/1.45 "Segoe UI","Helvetica Neue",Arial,sans-serif;}'
      + '.zy-calendar-stat-grid{display:flex;flex-wrap:nowrap;gap:8px;margin-top:12px;overflow-x:auto;overflow-y:hidden;padding-bottom:2px;scrollbar-width:thin;}'
      + '.zy-calendar-stat{flex:1 1 0;min-width:84px;padding:10px 12px;border-radius:14px;background:var(--zy-cal-panel-soft);border:1px solid rgba(226,232,240,.9);}'
      + '.zy-calendar-stat-label{display:block;font:700 10px/1.05 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-muted);letter-spacing:.05em;text-transform:uppercase;margin-bottom:4px;}'
      + '.zy-calendar-stat-value{display:block;font:700 15px/1.1 "Segoe UI","Helvetica Neue",Arial,sans-serif;word-break:break-word;}'
      + '.zy-calendar-event-meta{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:10px;margin-top:14px;}'
      + '.zy-calendar-event-meta-item{padding:11px 12px;border:1px solid rgba(226,232,240,.9);border-radius:14px;background:#f8fafc;}'
      + '.zy-calendar-event-meta-label{display:block;font:700 11px/1.1 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-muted);text-transform:uppercase;letter-spacing:.05em;margin-bottom:6px;}'
      + '.zy-calendar-event-meta-value{display:block;font:600 13px/1.35 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-text);word-break:break-word;}'
      + '.zy-calendar-event-actions{display:flex;flex-wrap:wrap;gap:8px;margin-top:14px;}'
      + '.zy-calendar-live-region{position:absolute;left:-9999px;top:auto;width:1px;height:1px;overflow:hidden;}'
      + '.zy-calendar-backdrop{position:fixed;inset:0;background:rgba(15,23,42,.42);display:none;align-items:flex-start;justify-content:center;padding:32px 18px;z-index:70;}'
      + '.zy-calendar-backdrop.is-open{display:flex;}'
      + '.zy-calendar-editor{position:relative;width:min(920px,100%);max-height:calc(100vh - 64px);overflow:auto;border-radius:24px;background:#fff;box-shadow:0 26px 80px rgba(15,23,42,.32);border:1px solid rgba(255,255,255,.7);}'
      + '.zy-calendar-host-workspace .zy-calendar-backdrop{top:calc(var(--clay-topbar-height,84px) + 12px);right:12px;bottom:12px;left:calc(var(--clay-sidebar-width,300px) + 12px);padding:0;align-items:stretch;justify-content:stretch;background:rgba(15,23,42,.28);backdrop-filter:blur(4px);}'
      + 'body.clay-shell-ready.clay-sidebar-collapsed .zy-calendar-host-workspace .zy-calendar-backdrop{left:calc(var(--clay-sidebar-collapsed-width,98px) + 12px);}'
      + '.zy-calendar-host-workspace .zy-calendar-editor{width:100%;max-width:none;height:100%;max-height:none;display:flex;flex-direction:column;overflow:hidden;border-radius:26px;}'
      + '.zy-calendar-host-workspace .zy-calendar-editor form{display:flex;flex-direction:column;min-height:0;height:100%;}'
      + '.zy-calendar-host-workspace .zy-calendar-editor-body{flex:1 1 auto;min-height:0;overflow:auto;padding-bottom:18px;}'
      + '.zy-calendar-host-workspace .zy-calendar-editor-footer{flex-shrink:0;}'
      + '.zy-calendar-editor-header{display:flex;flex-wrap:wrap;align-items:flex-start;justify-content:space-between;gap:12px;padding:22px 24px 18px;border-bottom:1px solid var(--zy-cal-border);background:#fbfcff;}'
      + '.zy-calendar-editor-title{font:700 24px/1.1 "Segoe UI","Helvetica Neue",Arial,sans-serif;letter-spacing:-.03em;margin:0;}'
      + '.zy-calendar-editor-body{padding:22px 24px;display:flex;flex-direction:column;gap:16px;}'
      + '.zy-calendar-editor-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;}'
      + '.zy-calendar-editor-field{display:flex;flex-direction:column;gap:7px;}'
      + '.zy-calendar-editor-label{font:700 12px/1.1 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:#334155;}'
      + '.zy-calendar-editor-input,.zy-calendar-editor-textarea,.zy-calendar-editor-select{border:1px solid var(--zy-cal-border);border-radius:14px;padding:11px 13px;background:#fff;color:var(--zy-cal-text);font:500 14px/1.3 "Segoe UI","Helvetica Neue",Arial,sans-serif;width:100%;}'
      + '.zy-calendar-editor-textarea{min-height:112px;resize:vertical;}'
      + '.zy-calendar-editor-inline{display:flex;gap:10px;align-items:center;flex-wrap:wrap;}'
      + '.zy-calendar-editor-checkbox{display:inline-flex;align-items:center;gap:8px;font:600 13px/1.2 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:#334155;}'
      + '.zy-calendar-editor-footer{display:flex;flex-wrap:wrap;gap:10px;align-items:center;justify-content:space-between;padding:18px 24px 24px;border-top:1px solid var(--zy-cal-border);background:#fff;}'
      + '.zy-calendar-editor-note{font:500 12px/1.35 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-muted);}'
      + '.zy-calendar-playlist-search{display:flex;flex-direction:column;gap:12px;margin-bottom:14px;}'
      + '.zy-calendar-playlist-results,.zy-calendar-playlist-list{display:flex;flex-direction:column;gap:10px;}'
      + '.zy-calendar-playlist-result,.zy-calendar-playlist-card{display:flex;flex-direction:column;gap:10px;padding:12px;border-radius:16px;border:1px solid rgba(226,232,240,.9);background:#f8fafc;}'
      + '.zy-calendar-playlist-result-head,.zy-calendar-playlist-card-head{display:flex;flex-wrap:wrap;align-items:flex-start;justify-content:space-between;gap:10px;}'
      + '.zy-calendar-playlist-title{font:700 14px/1.25 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-text);text-decoration:none;}'
      + '.zy-calendar-playlist-title:hover{text-decoration:underline;}'
      + '.zy-calendar-playlist-meta{font:500 12px/1.4 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-muted);}'
      + '.zy-calendar-playlist-events{display:flex;flex-wrap:wrap;gap:8px;}'
      + '.zy-calendar-playlist-event-chip{display:inline-flex;align-items:center;gap:6px;padding:6px 9px;border-radius:999px;background:#fff;border:1px solid rgba(203,213,225,.95);font:600 11px/1.2 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:#334155;text-decoration:none;}'
      + '.zy-calendar-playlist-event-chip:hover{text-decoration:none;border-color:rgba(79,70,229,.28);color:#3730a3;}'
      + '.zy-calendar-playlist-result-actions{display:flex;flex-wrap:wrap;gap:8px;}'
      + '.zy-calendar-button[disabled],.zy-calendar-view-button[disabled],.zy-calendar-export-button[disabled]{opacity:.55;cursor:not-allowed;}'
      + '.zy-calendar-choice-backdrop{position:absolute;inset:0;background:rgba(15,23,42,.42);display:none;align-items:center;justify-content:center;padding:18px;z-index:3;}'
      + '.zy-calendar-choice-backdrop.is-open{display:flex;}'
      + '.zy-calendar-choice-dialog{width:min(480px,100%);border-radius:20px;background:#fff;border:1px solid rgba(226,232,240,.9);box-shadow:0 24px 60px rgba(15,23,42,.24);padding:20px;display:flex;flex-direction:column;gap:14px;}'
      + '.zy-calendar-inline-message{display:none;padding:11px 13px;border-radius:14px;background:#eef2ff;color:#3730a3;font:600 13px/1.35 "Segoe UI","Helvetica Neue",Arial,sans-serif;}'
      + '.zy-calendar-inline-message.is-visible{display:block;}'
      + '.zy-calendar-inline-message.is-error{background:rgba(220,38,38,.1);color:#991b1b;}'
      + '.zy-calendar-inline-message.is-success{background:rgba(15,118,110,.1);color:#0f766e;}'
      + '.zy-calendar-loading{position:absolute;inset:0;background:rgba(255,255,255,.76);display:none;align-items:center;justify-content:center;font:700 14px/1 "Segoe UI","Helvetica Neue",Arial,sans-serif;color:#334155;backdrop-filter:blur(2px);z-index:2;}'
      + '.zy-calendar-loading.is-visible{display:flex;}'
      + '@media (max-width:1280px){.zy-calendar-body{grid-template-columns:minmax(0,1fr);}.zy-calendar-panel{order:-1;}.zy-calendar-canvas{height:min(72vh,820px);min-height:620px;}}'
      + '@media (max-width:1024px){.zy-calendar-host-workspace .zy-calendar-backdrop{top:calc(var(--clay-topbar-height,84px) + 10px);right:10px;bottom:10px;left:10px;}}'
      + '@media (max-width:720px){.zy-calendar-toolbar{gap:6px;padding-bottom:2px;}.zy-calendar-view-switcher{display:none;}.zy-calendar-mobile-view-field,.zy-calendar-mobile-view-select{display:inline-flex;}.zy-calendar-mobile-view-select{display:block;}.zy-calendar-toolbar-menu-popover{right:auto;left:0;}.zy-calendar-utility-backdrop{padding:18px 10px 10px;}.zy-calendar-body{padding:14px;gap:14px;}.zy-calendar-canvas{height:68vh;min-height:560px;}.zy-calendar-editor-grid{grid-template-columns:minmax(0,1fr);}.zy-calendar-editor-header,.zy-calendar-editor-body,.zy-calendar-editor-footer{padding-left:16px;padding-right:16px;}.zy-calendar-host-workspace .zy-calendar-backdrop{top:calc(var(--clay-topbar-height,84px) + 8px);right:8px;bottom:8px;left:8px;}.zy-calendar-host-workspace .zy-calendar-editor{border-radius:24px;}}';
    document.head.appendChild(style);
  }

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

  function copy(value) {
    return JSON.parse(JSON.stringify(value));
  }

  function escapeHtml(value) {
    return String(value || '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#039;');
  }

  function padNumber(value) {
    return String(value).padStart(2, '0');
  }

  function ensureDateKey(value) {
    var safeValue = asText(value);
    return /^\d{4}-\d{2}-\d{2}$/.test(safeValue) ? safeValue : DateMath.todayKey();
  }

  function normalizeIsoString(value) {
    var safeValue = asText(value);
    if (safeValue === '') {
      return '';
    }

    var date = new Date(safeValue);
    return Number.isNaN(date.getTime()) ? '' : date.toISOString();
  }

  function minutesToClockLabel(minutes) {
    var safeMinutes = Math.max(0, Math.round(minutes));
    var hour = Math.floor(safeMinutes / 60) % 24;
    var minute = safeMinutes % 60;
    var suffix = hour >= 12 ? 'PM' : 'AM';
    var labelHour = hour % 12;
    if (labelHour === 0) {
      labelHour = 12;
    }
    return labelHour + ':' + padNumber(minute) + ' ' + suffix;
  }

  function formatterKey(locale, timeZone, options) {
    return locale + '|' + timeZone + '|' + JSON.stringify(options || {});
  }

  function getFormatter(locale, timeZone, options) {
    var safeLocale = asText(locale) || 'en-US';
    var safeTimeZone = asText(timeZone) || 'UTC';
    var key = formatterKey(safeLocale, safeTimeZone, options);
    if (!formatterCache[key]) {
      formatterCache[key] = new Intl.DateTimeFormat(safeLocale, Object.assign({}, options || {}, {
        timeZone: safeTimeZone
      }));
    }
    return formatterCache[key];
  }

  function getZonedParts(dateValue, timeZone, locale) {
    var date = dateValue instanceof Date ? dateValue : new Date(dateValue);
    if (Number.isNaN(date.getTime())) {
      return null;
    }

    var parts = getFormatter(locale, timeZone, {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hourCycle: 'h23'
    }).formatToParts(date);
    var result = {};
    parts.forEach(function(part) {
      if (part.type === 'literal') {
        return;
      }
      result[part.type] = part.value;
    });
    return {
      year: parseInt(result.year || '0', 10),
      month: parseInt(result.month || '1', 10),
      day: parseInt(result.day || '1', 10),
      hour: parseInt(result.hour || '0', 10),
      minute: parseInt(result.minute || '0', 10),
      second: parseInt(result.second || '0', 10)
    };
  }

  function zonedPartsToDateKey(parts) {
    var safeParts = safeObject(parts);
    return String(safeParts.year || 0).padStart(4, '0') + '-' + padNumber(safeParts.month || 1) + '-' + padNumber(safeParts.day || 1);
  }

  function getDateKeyFromIso(utcIso, timeZone, locale) {
    var parts = getZonedParts(utcIso, timeZone, locale);
    return parts ? zonedPartsToDateKey(parts) : DateMath.todayKey();
  }

  function getMinutesFromIso(utcIso, timeZone, locale) {
    var parts = getZonedParts(utcIso, timeZone, locale);
    return parts ? ((parts.hour * 60) + parts.minute) : 0;
  }

  function formatDateKeyLabel(dateKey, locale, timeZone, options) {
    var parts = DateMath.parseDateKey(dateKey);
    if (!parts) {
      return dateKey;
    }

    var utcDate = new Date(Date.UTC(parts.year, parts.month - 1, parts.day, 12, 0, 0));
    return getFormatter(locale, timeZone, options || {
      weekday: 'short',
      month: 'short',
      day: 'numeric'
    }).format(utcDate);
  }

  function formatDateTimeLabel(utcIso, timeZone, locale) {
    var safeIso = normalizeIsoString(utcIso);
    if (safeIso === '') {
      return 'Not scheduled';
    }

    return getFormatter(locale, timeZone, {
      weekday: 'short',
      month: 'short',
      day: 'numeric',
      hour: 'numeric',
      minute: '2-digit'
    }).format(new Date(safeIso));
  }

  function formatRangeLabel(startUtc, endUtc, allDay, timeZone, locale) {
    var safeStart = normalizeIsoString(startUtc);
    if (safeStart === '') {
      return 'Not scheduled';
    }

    if (allDay) {
      var startKey = getDateKeyFromIso(safeStart, timeZone, locale);
      var endKey = normalizeIsoString(endUtc) !== '' ? getDateKeyFromIso(endUtc, timeZone, locale) : startKey;
      if (endKey === startKey) {
        return formatDateKeyLabel(startKey, locale, timeZone, {
          weekday: 'short',
          month: 'short',
          day: 'numeric'
        }) + ' | All day';
      }
      return formatDateKeyLabel(startKey, locale, timeZone, {
        month: 'short',
        day: 'numeric'
      }) + ' - ' + formatDateKeyLabel(endKey, locale, timeZone, {
        month: 'short',
        day: 'numeric'
      }) + ' | All day';
    }

    var safeEnd = normalizeIsoString(endUtc);
    if (safeEnd === '') {
      return formatDateTimeLabel(safeStart, timeZone, locale);
    }

    var startDateKey = getDateKeyFromIso(safeStart, timeZone, locale);
    var endDateKey = getDateKeyFromIso(safeEnd, timeZone, locale);
    if (startDateKey === endDateKey) {
      return formatDateKeyLabel(startDateKey, locale, timeZone, {
        weekday: 'short',
        month: 'short',
        day: 'numeric'
      }) + ' | ' + minutesToClockLabel(getMinutesFromIso(safeStart, timeZone, locale)) + ' - ' + minutesToClockLabel(getMinutesFromIso(safeEnd, timeZone, locale));
    }

    return formatDateTimeLabel(safeStart, timeZone, locale) + ' - ' + formatDateTimeLabel(safeEnd, timeZone, locale);
  }

  function formatRangeLabelLines(startUtc, endUtc, allDay, timeZone, locale) {
    var safeStart = normalizeIsoString(startUtc);
    if (safeStart === '') {
      return ['Not scheduled'];
    }

    if (allDay) {
      var startKey = getDateKeyFromIso(safeStart, timeZone, locale);
      var endKey = normalizeIsoString(endUtc) !== '' ? getDateKeyFromIso(endUtc, timeZone, locale) : startKey;
      if (endKey === startKey) {
        return [
          formatDateKeyLabel(startKey, locale, timeZone, {
            weekday: 'short',
            month: 'short',
            day: 'numeric'
          }),
          'All day'
        ];
      }
      return [
        formatDateKeyLabel(startKey, locale, timeZone, {
          month: 'short',
          day: 'numeric'
        }) + ' - ' + formatDateKeyLabel(endKey, locale, timeZone, {
          month: 'short',
          day: 'numeric'
        }),
        'All day'
      ];
    }

    var safeEnd = normalizeIsoString(endUtc);
    var startDateKey = getDateKeyFromIso(safeStart, timeZone, locale);
    if (safeEnd === '') {
      return [
        formatDateKeyLabel(startDateKey, locale, timeZone, {
          weekday: 'short',
          month: 'short',
          day: 'numeric'
        }),
        minutesToClockLabel(getMinutesFromIso(safeStart, timeZone, locale))
      ];
    }

    var endDateKey = getDateKeyFromIso(safeEnd, timeZone, locale);
    if (startDateKey === endDateKey) {
      return [
        formatDateKeyLabel(startDateKey, locale, timeZone, {
          weekday: 'short',
          month: 'short',
          day: 'numeric'
        }),
        minutesToClockLabel(getMinutesFromIso(safeStart, timeZone, locale)) + ' - ' + minutesToClockLabel(getMinutesFromIso(safeEnd, timeZone, locale))
      ];
    }

    return [
      formatDateTimeLabel(safeStart, timeZone, locale),
      formatDateTimeLabel(safeEnd, timeZone, locale)
    ];
  }

  function renderListRangeLabel(startUtc, endUtc, allDay, timeZone, locale) {
    return '<span class="zy-calendar-list-row-time">'
      + formatRangeLabelLines(startUtc, endUtc, allDay, timeZone, locale).map(function(line) {
        return '<span class="zy-calendar-list-row-time-line">' + escapeHtml(line) + '</span>';
      }).join('')
      + '</span>';
  }

  function renderCalendarActionIcon(name) {
    if (name === 'edit') {
      return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M12 20h9"/><path d="M16.5 3.5a2.12 2.12 0 1 1 3 3L7 19l-4 1 1-4Z"/></svg>';
    }
    return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M1 12s4-7 11-7 11 7 11 7-4 7-11 7S1 12 1 12Z"/><circle cx="12" cy="12" r="3"/></svg>';
  }

  function renderCalendarToolbarIcon(name) {
    if (name === 'help') {
      return '<svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M9.5 9a2.5 2.5 0 1 1 4.1 1.95c-.9.72-1.6 1.3-1.6 2.55"/><path d="M12 17.25h.01"/></svg>';
    }
    if (name === 'settings') {
      return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M21.7 7.3a6 6 0 0 1-7.8 7.8l-8.6 8.6a2 2 0 0 1-2.8-2.8l8.6-8.6a6 6 0 0 1 7.8-7.8l-3.2 3.2 2.8 2.8Z"/><circle cx="5.5" cy="18.5" r=".75"/></svg>';
    }
    return '<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M4 7h16"/><path d="M4 12h16"/><path d="M4 17h16"/></svg>';
  }

  function renderCalendarListActionButton(config) {
    var settings = safeObject(config);
    var label = asText(settings.label) || 'Action';
    return '<button type="button" class="zy-calendar-list-icon-button'
      + (settings.primary ? ' zy-calendar-list-icon-button-primary' : '')
      + '" data-action="' + escapeHtml(asText(settings.action)) + '" data-event-id="' + escapeHtml(asText(settings.eventId)) + '" aria-label="' + escapeHtml(label) + '" title="' + escapeHtml(label) + '">'
      + renderCalendarActionIcon(asText(settings.icon))
      + '</button>';
  }

  function renderCalendarToolbarIconButton(action, label, iconName, isPrimary, extraAttributes) {
    return '<button type="button" class="zy-calendar-button zy-calendar-toolbar-icon-button'
      + (isPrimary ? ' zy-calendar-button-primary' : '')
      + '" data-action="' + escapeHtml(asText(action)) + '" aria-label="' + escapeHtml(asText(label)) + '" title="' + escapeHtml(asText(label)) + '"'
      + (asText(extraAttributes) ? (' ' + asText(extraAttributes)) : '')
      + '>'
      + renderCalendarToolbarIcon(asText(iconName))
      + '</button>';
  }

  function renderCalendarExportMenuItem(format) {
    var safeFormat = asText(format).toLowerCase();
    var label = safeFormat === 'xlsx' ? 'XLSX' : 'CSV';
    return '<button type="button" class="zy-calendar-toolbar-menu-item" data-action="export-' + escapeHtml(safeFormat) + '" aria-label="Download ' + escapeHtml(label) + '" title="Download ' + escapeHtml(label) + '" role="menuitem">'
      + '<svg class="export-trigger-icon" viewBox="0 0 20 20" fill="currentColor" aria-hidden="true">'
      + '<path d="M10 2a1 1 0 0 1 1 1v7.59l2.3-2.3a1 1 0 1 1 1.4 1.42l-4 4a1 1 0 0 1-1.4 0l-4-4a1 1 0 1 1 1.4-1.42l2.3 2.3V3a1 1 0 0 1 1-1Z"></path>'
      + '<path d="M3 15a1 1 0 0 1 1-1h12a1 1 0 1 1 0 2H4a1 1 0 0 1-1-1Z"></path>'
      + '</svg>'
      + '<span class="export-trigger-label">' + label + '</span>'
      + '</button>';
  }

  function renderCalendarAddEventButton() {
    return '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="add-event" aria-label="Add event" title="Add event">'
      + '<span class="zy-calendar-toolbar-event-plus" aria-hidden="true">+</span>'
      + '<span>Event</span>'
      + '</button>';
  }

  function toLocalInputValue(utcIso, timeZone, locale) {
    var safeIso = normalizeIsoString(utcIso);
    if (safeIso === '') {
      return '';
    }

    var parts = getZonedParts(safeIso, timeZone, locale);
    if (!parts) {
      return '';
    }

    return String(parts.year).padStart(4, '0') + '-' + padNumber(parts.month) + '-' + padNumber(parts.day) + 'T' + padNumber(parts.hour) + ':' + padNumber(parts.minute);
  }

  function parseLocalInputValue(value) {
    var match = /^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})$/.exec(asText(value));
    if (!match) {
      return null;
    }

    return {
      year: parseInt(match[1], 10),
      month: parseInt(match[2], 10),
      day: parseInt(match[3], 10),
      hour: parseInt(match[4], 10),
      minute: parseInt(match[5], 10),
      second: 0
    };
  }

  function zonedLocalToUtcIso(parts, timeZone, locale) {
    var safeParts = safeObject(parts);
    var guess = Date.UTC(safeParts.year || 2000, (safeParts.month || 1) - 1, safeParts.day || 1, safeParts.hour || 0, safeParts.minute || 0, safeParts.second || 0);
    var target = Date.UTC(safeParts.year || 2000, (safeParts.month || 1) - 1, safeParts.day || 1, safeParts.hour || 0, safeParts.minute || 0, safeParts.second || 0);

    for (var index = 0; index < 5; index += 1) {
      var zoned = getZonedParts(new Date(guess), timeZone, locale);
      if (!zoned) {
        break;
      }
      var rendered = Date.UTC(zoned.year, zoned.month - 1, zoned.day, zoned.hour, zoned.minute, zoned.second);
      var diff = target - rendered;
      guess += diff;
      if (diff === 0) {
        break;
      }
    }

    return new Date(guess).toISOString();
  }

  function localInputToUtcIso(value, timeZone, locale) {
    var parts = parseLocalInputValue(value);
    if (!parts) {
      return '';
    }

    return zonedLocalToUtcIso(parts, timeZone, locale);
  }

  function buildUtcIsoFromDateKeyMinutes(dateKey, minutes, timeZone, locale) {
    var parsed = DateMath.parseDateKey(dateKey);
    if (!parsed) {
      return '';
    }

    var safeMinutes = Math.max(0, Math.round(minutes));
    return zonedLocalToUtcIso({
      year: parsed.year,
      month: parsed.month,
      day: parsed.day,
      hour: Math.floor(safeMinutes / 60),
      minute: safeMinutes % 60,
      second: 0
    }, timeZone, locale);
  }

  function addMinutesToIso(utcIso, minutes) {
    var safeIso = normalizeIsoString(utcIso);
    if (safeIso === '') {
      return '';
    }

    return new Date(new Date(safeIso).getTime() + (minutes * 60000)).toISOString();
  }

  function addDaysToIso(utcIso, days) {
    return addMinutesToIso(utcIso, days * 1440);
  }

  function durationMinutes(event) {
    var start = new Date(normalizeIsoString(event.startUtc)).getTime();
    var end = new Date(normalizeIsoString(event.endUtc)).getTime();
    if (!Number.isFinite(start) || !Number.isFinite(end) || end <= start) {
      return event.allDay ? 1440 : 60;
    }
    return Math.max(15, Math.round((end - start) / 60000));
  }

  function createLocalEventId() {
    if (window.crypto && typeof window.crypto.randomUUID === 'function') {
      return window.crypto.randomUUID();
    }

    return 'evt_' + Math.random().toString(36).slice(2, 10) + '_' + Date.now().toString(36);
  }

  function normalizeEvent(input, fallbackTimezone) {
    var source = safeObject(input);
    var timeZone = asText(source.timezone || source.timezoneName || fallbackTimezone || 'UTC') || 'UTC';
    var startUtc = normalizeIsoString(source.startUtc || source.scheduledStartUtc);
    var endUtc = normalizeIsoString(source.endUtc || source.scheduledEndUtc);
    var allDay = !!source.allDay;

    if (startUtc === '') {
      startUtc = new Date().toISOString();
    }
    if (endUtc === '') {
      endUtc = addMinutesToIso(startUtc, allDay ? 1440 : 60);
    }
    if (new Date(endUtc).getTime() <= new Date(startUtc).getTime()) {
      endUtc = addMinutesToIso(startUtc, allDay ? 1440 : 60);
    }

    return {
      id: asText(source.id || source.eventId) || createLocalEventId(),
      eventId: asText(source.eventId || source.id) || createLocalEventId(),
      title: asText(source.title) || 'Untitled event',
      description: asText(source.description),
      startUtc: startUtc,
      endUtc: endUtc,
      allDay: allDay,
      timezone: timeZone,
      timezoneName: timeZone,
      location: asText(source.location || source.locationLabel),
      locationLabel: asText(source.locationLabel || source.location),
      locationAddress: asText(source.locationAddress),
      locationLat: source.locationLat === null || source.locationLat === undefined || source.locationLat === '' ? null : asNumber(source.locationLat, 0),
      locationLng: source.locationLng === null || source.locationLng === undefined || source.locationLng === '' ? null : asNumber(source.locationLng, 0),
      category: asText(source.category || source.eventType) || 'Concert',
      color: /^#[0-9a-fA-F]{6}$/.test(asText(source.color)) ? asText(source.color).toLowerCase() : '#4f46e5',
      readOnly: !!source.readOnly,
      eventType: asText(source.eventType || source.category) || 'Concert',
      status: asText(source.status) || 'Draft',
      customerName: asText(source.customerName),
      customerEmail: asText(source.customerEmail),
      customerPhone: asText(source.customerPhone),
      priceAmount: source.priceAmount === null || source.priceAmount === undefined || source.priceAmount === '' ? null : asNumber(source.priceAmount, null),
      currency: asText(source.currency) || 'USD',
      notes: asText(source.notes),
      logisticsNote: asText(source.logisticsNote),
      linkedPlaylistCount: Math.max(0, parseInt(source.linkedPlaylistCount || 0, 10) || 0),
      linkedPlaylists: safeArray(source.linkedPlaylists),
      checklistItemCount: Math.max(0, parseInt(source.checklistItemCount || 0, 10) || 0),
      checklistRows: safeArray(source.checklistRows),
      repositoryId: asText(source.repositoryId),
      currentCommitSha256: asText(source.currentCommitSha256),
      playlistsBuilderUrl: asText(source.playlistsBuilderUrl),
      createdUtc: normalizeIsoString(source.createdUtc),
      updatedUtc: normalizeIsoString(source.updatedUtc)
    };
  }

  function pluralize(count, singular, plural) {
    return count === 1 ? singular : (plural || singular + 's');
  }

  function formatConnectionLabel(event) {
    var safeEvent = safeObject(event);
    var title = asText(safeEvent.title) || 'Event';
    var scheduledStartUtc = asText(safeEvent.scheduledStartUtc);
    if (scheduledStartUtc === '') {
      return title;
    }

    return title + ' | ' + scheduledStartUtc.slice(0, 16).replace('T', ' ');
  }

  function compareEvents(left, right) {
    var leftStart = new Date(left.startUtc).getTime();
    var rightStart = new Date(right.startUtc).getTime();
    if (leftStart !== rightStart) {
      return leftStart - rightStart;
    }

    var leftEnd = new Date(left.endUtc).getTime();
    var rightEnd = new Date(right.endUtc).getTime();
    if (leftEnd !== rightEnd) {
      return rightEnd - leftEnd;
    }

    return asText(left.title).localeCompare(asText(right.title));
  }

  function getEventSpan(event, timeZone, locale) {
    var safeEvent = safeObject(event);
    var startKey = getDateKeyFromIso(safeEvent.startUtc, timeZone, locale);
    var endKey = getDateKeyFromIso(safeEvent.endUtc, timeZone, locale);
    var endMinutes = getMinutesFromIso(safeEvent.endUtc, timeZone, locale);
    if (safeEvent.allDay && compareDateKeys(endKey, startKey) >= 0) {
      if (endMinutes === 0 && compareDateKeys(endKey, startKey) > 0) {
        endKey = DateMath.addDateDays(endKey, -1);
      }
      return {
        startKey: startKey,
        endKey: endKey
      };
    }

    if (compareDateKeys(endKey, startKey) < 0) {
      endKey = startKey;
    }

    return {
      startKey: startKey,
      endKey: endKey
    };
  }

  function compareDateKeys(left, right) {
    return DateMath.compareDateKeys(ensureDateKey(left), ensureDateKey(right));
  }

  function eventSpansDate(event, dateKey, timeZone, locale) {
    var span = getEventSpan(event, timeZone, locale);
    var safeDateKey = ensureDateKey(dateKey);
    return compareDateKeys(span.startKey, safeDateKey) <= 0 && compareDateKeys(span.endKey, safeDateKey) >= 0;
  }

  function eventIntersectsRange(event, startKey, endKey, timeZone, locale) {
    var span = getEventSpan(event, timeZone, locale);
    return compareDateKeys(span.endKey, startKey) >= 0 && compareDateKeys(span.startKey, endKey) <= 0;
  }

  function buildDensityMap(events, timeZone, locale) {
    var density = {};
    safeArray(events).forEach(function(event) {
      var span = getEventSpan(event, timeZone, locale);
      var cursor = span.startKey;
      while (compareDateKeys(cursor, span.endKey) <= 0) {
        density[cursor] = (density[cursor] || 0) + 1;
        cursor = DateMath.addDateDays(cursor, 1);
      }
    });
    return density;
  }

  function buildTimeZoneList(currentValue, extraValues) {
    var list = [];
    if (typeof Intl.supportedValuesOf === 'function') {
      try {
        list = Intl.supportedValuesOf('timeZone');
      } catch (_) {
        list = [];
      }
    }

    if (!Array.isArray(list) || list.length === 0) {
      list = TIMEZONE_FALLBACKS.slice();
    }

    var items = list.concat(safeArray(extraValues), [currentValue]);
    var seen = {};
    return items.filter(function(value) {
      var safeValue = asText(value);
      if (safeValue === '' || seen[safeValue]) {
        return false;
      }
      seen[safeValue] = true;
      return true;
    }).sort(function(left, right) {
      return left.localeCompare(right);
    });
  }

  function buildDefaultEvent(timeZone, locale, dateKey, startMinutes, allDay) {
    var safeDateKey = ensureDateKey(dateKey);
    var safeMinutes = allDay ? 0 : Math.max(0, Math.round(startMinutes));
    var startUtc = buildUtcIsoFromDateKeyMinutes(safeDateKey, safeMinutes, timeZone, locale);
    return normalizeEvent({
      id: '',
      eventId: '',
      title: '',
      description: '',
      startUtc: startUtc,
      endUtc: addMinutesToIso(startUtc, allDay ? 1440 : 60),
      allDay: allDay,
      timezone: timeZone,
      category: 'Concert',
      eventType: 'Concert',
      status: 'Draft',
      color: '#4f46e5',
      readOnly: false,
      currency: 'USD'
    }, timeZone);
  }

  function formatPeriodLabel(view, anchorDateKey, weekStartsOn) {
    var safeView = asText(view);
    var safeAnchor = ensureDateKey(anchorDateKey);
    var anchorParts = DateMath.parseDateKey(safeAnchor);
    if (!anchorParts) {
      return safeAnchor;
    }

    if (safeView === 'day') {
      return DAY_SHORT[DateMath.dayOfWeek(safeAnchor)] + ', ' + MONTH_SHORT[anchorParts.month - 1] + ' ' + anchorParts.day + ', ' + anchorParts.year;
    }
    if (safeView === 'week') {
      var weekStart = DateMath.startOfWeek(safeAnchor, weekStartsOn);
      var weekEnd = DateMath.endOfWeek(safeAnchor, weekStartsOn);
      var startParts = DateMath.parseDateKey(weekStart);
      var endParts = DateMath.parseDateKey(weekEnd);
      if (startParts && endParts) {
        if (startParts.month === endParts.month) {
          return MONTH_SHORT[startParts.month - 1] + ' ' + startParts.day + ' - ' + endParts.day + ', ' + endParts.year;
        }
        return MONTH_SHORT[startParts.month - 1] + ' ' + startParts.day + ' - ' + MONTH_SHORT[endParts.month - 1] + ' ' + endParts.day + ', ' + endParts.year;
      }
    }
    if (safeView === 'month') {
      return DateMath.monthLabel(safeAnchor);
    }
    if (safeView === 'year') {
      return String(anchorParts.year);
    }
    return safeView === 'list' ? 'List view' : DateMath.monthLabel(safeAnchor);
  }

  function scopeRange(scope, anchorDateKey, weekStartsOn) {
    var safeScope = asText(scope);
    var safeAnchor = ensureDateKey(anchorDateKey);
    if (safeScope === 'day') {
      return {
        startKey: safeAnchor,
        endKey: safeAnchor
      };
    }
    if (safeScope === 'week') {
      return {
        startKey: DateMath.startOfWeek(safeAnchor, weekStartsOn),
        endKey: DateMath.endOfWeek(safeAnchor, weekStartsOn)
      };
    }
    if (safeScope === 'month') {
      return {
        startKey: DateMath.startOfMonth(safeAnchor),
        endKey: DateMath.endOfMonth(safeAnchor)
      };
    }
    var parts = DateMath.parseDateKey(safeAnchor) || { year: new Date().getUTCFullYear() };
    return {
      startKey: parts.year + '-01-01',
      endKey: parts.year + '-12-31'
    };
  }

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
      + renderCalendarAddEventButton()
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
    this.utilityBody.innerHTML = ''
      + '<p>The calendar is optimized for fast editing directly on the canvas.</p>'
      + '<ul class="zy-calendar-utility-list">'
      + '<li>Double click empty space to create a timed event for that date.</li>'
      + '<li>Drag timed blocks in day or week view to move them.</li>'
      + '<li>Resize event edges to change duration.</li>'
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
      this.panelActions.innerHTML = ''
        + '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="edit-selected">Edit selected</button>'
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
    this.panelMeta.innerHTML = ''
      + this.renderMeta('Current view', this.state.view === 'list' ? ('List / ' + this.state.listScope) : this.state.view)
      + this.renderMeta('Anchor date', this.state.anchorDateKey)
      + this.renderMeta('Display timezone', this.state.timezone)
      + this.renderMeta('Locale', this.state.locale)
      + this.renderMeta('Keyboard', 'Arrows move, Enter edits, Delete removes')
      + this.renderMeta('Create', 'Double click empty space or use Add event');
    this.panelActions.innerHTML = ''
      + '<button type="button" class="zy-calendar-button zy-calendar-button-primary" data-action="add-event">Add event</button>'
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
      return '<tr>'
        + '<td class="zy-calendar-list-col-actions"><div class="zy-calendar-list-actions">'
        + renderCalendarListActionButton({
          action: 'select-row',
          eventId: eventId,
          label: 'Select event',
          icon: 'view'
        })
        + renderCalendarListActionButton({
          action: 'edit-row',
          eventId: eventId,
          label: 'Edit event',
          icon: 'edit',
          primary: true
        })
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
      if (selectedEvent) {
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
        if (this.options.allowResize && !item.event.readOnly) {
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
      cursor = this.options.allowDragDrop ? 'grab' : 'pointer';
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

    if ((region.type === 'resize-start' || region.type === 'resize-end') && selectedEvent && this.options.allowResize && !selectedEvent.readOnly) {
      this.state.interaction = {
        type: region.type,
        event: selectedEvent,
        startPoint: point,
        moved: false
      };
      event.preventDefault();
      return;
    }

    if (region.type === 'timed-event' && selectedEvent && this.options.allowDragDrop && !selectedEvent.readOnly) {
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

    if (region.type === 'all-day-event' && selectedEvent && this.options.allowDragDrop && !selectedEvent.readOnly) {
      this.state.interaction = {
        type: 'move-day-span',
        event: selectedEvent,
        startPoint: point,
        moved: false
      };
      event.preventDefault();
      return;
    }

    if (region.type === 'month-event' && selectedEvent && this.options.allowDragDrop && !selectedEvent.readOnly) {
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
      if (selectedEvent) {
        this.openEditor(selectedEvent, 'edit');
      }
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
      if (selectedEvent) {
        this.openEditor(selectedEvent, 'edit');
      } else {
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
})();
