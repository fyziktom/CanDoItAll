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
      + '.zy-calendar-shell{--zy-cal-border:rgba(15,23,42,.09);--zy-cal-border-strong:rgba(79,70,229,.24);--zy-cal-bg:#f5f7fb;--zy-cal-panel:#ffffff;--zy-cal-panel-soft:#f8fafc;--zy-cal-text:#0f172a;--zy-cal-muted:#64748b;--zy-cal-accent:#4f46e5;--zy-cal-accent-soft:rgba(79,70,229,.12);--zy-cal-success:#0f766e;--zy-cal-danger:#dc2626;--zy-cal-shadow:0 18px 40px rgba(15,23,42,.08);container-type:inline-size;font-family:"Segoe UI","Helvetica Neue",Arial,sans-serif;color:var(--zy-cal-text);}'
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
      + '@container (max-width:1280px){.zy-calendar-body{grid-template-columns:minmax(0,1fr);}.zy-calendar-panel{order:-1;}.zy-calendar-canvas{height:min(72vh,820px);min-height:620px;}}'
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

  var shared = window.ZyCanvasCalendarModule = window.ZyCanvasCalendarModule || {};
  Object.assign(shared, { CanvasSurface, HitRegistry, DateMath, drawMiniMonth, drawTimedGrid, fillRoundedPanel, fitText, wrapText, STYLE_ID, DAY_SHORT, MONTH_SHORT, TIMEZONE_FALLBACKS, injectStyles, asText, asNumber, clamp, safeObject, safeArray, copy, escapeHtml, padNumber, ensureDateKey, normalizeIsoString, minutesToClockLabel, formatterKey, getFormatter, getZonedParts, zonedPartsToDateKey, getDateKeyFromIso, getMinutesFromIso, formatDateKeyLabel, formatDateTimeLabel, formatRangeLabel, formatRangeLabelLines, renderListRangeLabel, renderCalendarActionIcon, renderCalendarToolbarIcon, renderCalendarListActionButton, renderCalendarToolbarIconButton, renderCalendarExportMenuItem, renderCalendarAddEventButton, toLocalInputValue, parseLocalInputValue, zonedLocalToUtcIso, localInputToUtcIso, buildUtcIsoFromDateKeyMinutes, addMinutesToIso, addDaysToIso, durationMinutes, createLocalEventId, normalizeEvent, pluralize, formatConnectionLabel, compareEvents, getEventSpan, compareDateKeys, eventSpansDate, eventIntersectsRange, buildDensityMap, buildTimeZoneList, buildDefaultEvent, formatPeriodLabel, scopeRange });
})();
