# ScalanceLogs — Syslog Viewer

WPF desktop application for collecting and viewing UDP syslog from Siemens Scalance XC-200 industrial switches.

```
Scalance XC-200  ──UDP 514──►  ScalanceLogs.exe  ──►  logs\
                                      │
                               System tray + WPF UI
```

---

## Requirements

- Windows 10 / 11
- .NET 9 Runtime ([download](https://dotnet.microsoft.com/download/dotnet/9))
- Administrator rights if using UDP port 514 (not needed for port 5140)

---

## Quick Start

1. Run `ScalanceLogs.exe`
2. Open **Settings → General** — set UDP port and log folder
3. Open **Settings → Switches** — add switch IPs and names
4. Configure the switch WBM to send syslog to this PC (see below)
5. Messages appear in the live panel and center log view in real time

The app minimizes to the system tray on close. Use tray → **Exit** to quit.

---

## UI Layout

```
┌─────────────────────────────────────────────────────────────────┐
│  Toolbar: Search │ Quick Filters │ Line count │ Refresh │ Settings │
├──────────┬──────────────────────────────────┬───────────────────┤
│          │  Timestamp  Severity  Host  Msg  │  LIVE EVENTS      │
│ LOG FILES│  ...                             │  SWITCHES         │
│          │  ...                             │  ...              │
│ 2026-04-25│ ...                             │                   │
│  events  │                                  │                   │
│  SW-01   │                                  │                   │
└──────────┴──────────────────────────────────┴───────────────────┘
│ footer: selected file                                      status │
```

- **Left sidebar** — log files grouped by date; click to load into center panel
- **Center panel** — log entries with severity chip, host, message; click `▸` to expand raw line
- **Right panel** — live feed of incoming messages with fade-out; active switches list
- Both side panels are collapsible; the live panel flashes when a message arrives while collapsed

---

## Settings

All settings are stored in `%APPDATA%\ScalanceLogs\settings.json` and configured through the UI.

### General

| Setting | Description |
|---|---|
| UDP Port | Port to listen on. 514 = standard (needs admin). 5140 = unprivileged. |
| Retention (days) | Log files older than this are deleted automatically |
| Log folder | Where `.log` files are written. Relative path = next to exe; absolute path supported |
| Start with Windows | Adds / removes registry autostart entry |
| Balloon notifications | Show toast notification on new messages; configurable per severity label |

**Test packet** — sends a test UDP syslog to the configured port to verify the collector is running.

### Switches

Maps source IP addresses to friendly names. The name appears in log filenames and the UI host column.

If a switch IP is not listed — messages are still collected using the raw IP as the name.

### Event Filters

Regex patterns. Messages matching **any** pattern are written to `events_YYYY-MM-DD.log`.  
If the list is empty — **all** messages go to the events log.

### Message Types

Custom severity chip label and color, matched against message text (first match wins).  
Useful for distinguishing `LINK DOWN` / `LINK UP` / `FAULT` visually.

| Column | Description |
|---|---|
| Pattern (regex) | Matched against the message text |
| Label | Text shown in the severity chip |
| Color | Chip text color (`#rrggbb` or `rgba(...)`) |
| Background | Chip and row background color |

### Quick Filters

Buttons shown in the toolbar for one-click search. Label = button text, Query = search string.

---

## Log Files

Written to the configured log folder, one file per switch per day:

```
logs\
├── events_2026-04-25.log                         # filtered events, all switches
├── 192_168_1_10_SW-Panel-A_2026-04-25.log        # all messages, per switch (named)
├── 192_168_1_11_2026-04-25.log                   # all messages, per switch (no name configured)
└── ...
```

Log line format:
```
2026-04-25 10:23:01 [NOTICE] 192.168.1.10 (SW-Panel-A) | Link up on port P0.3
```

Old files are deleted automatically on startup and daily at midnight.

---

## Switch WBM Setup

```
System → Time → SNTP Client
  Server Address:  <IP of this PC>

System → Syslog
  Server Address:  <IP of this PC>
  Port:            514   (or 5140 if not running as Administrator)
  Severity:        Informational
```

---

## Security

- **IP hyperlinks** — host column shows the source IP as a clickable link (`https://<ip>`).  
  Only valid IPv4 addresses are made clickable; any other host string is shown as plain text.  
  The click handler validates the scheme is `https` and the host parses as an IP before opening a browser — crafted syslog messages cannot trigger arbitrary URLs or commands.

- **Network** — the UDP listener accepts packets from any source (no IP whitelist in the current version).  
  Bind to a specific interface if the host has multiple NICs and exposure is a concern.

---

## Building from Source

```
dotnet build csharp -c Release
```

Output: `csharp\bin\Release\net9.0-windows\`

Requires .NET 9 SDK.

---

## Legacy Implementations

The `python\` and `powershell\` folders contain earlier collector + HTTP viewer implementations.  
They write the same log file format and can share the same `logs\` folder with the C# app.  
The C# WPF app supersedes them for day-to-day use.

---

## Author

IVTI
