# SW-LOG — Syslog Viewer

[![Build](https://github.com/LuckyCase/SyslogViewer/actions/workflows/build.yml/badge.svg)](https://github.com/LuckyCase/SyslogViewer/actions/workflows/build.yml)
[![Latest release](https://img.shields.io/github/v/release/LuckyCase/SyslogViewer?include_prereleases&label=download)](https://github.com/LuckyCase/SyslogViewer/releases/latest)
[![.NET](https://img.shields.io/badge/.NET-6.0%20%7C%209.0-512BD4?logo=dotnet&logoColor=white)](#requirements)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4?logo=windows&logoColor=white)](#requirements)

WPF desktop application for collecting and viewing UDP syslog from industrial Ethernet switches and any standard syslog source (RFC 5424 / RFC 3164). Tested with Siemens Scalance® XC-200 series.

```
Industrial switches  ──UDP──►  SW-LOG  ──►  rotating log files
                                  │
                            tray icon + WPF UI (live + history)
```

## Download

Get the latest self-contained build from the [**Releases page**](https://github.com/LuckyCase/SyslogViewer/releases/latest) — no .NET install needed:

| File | When to pick |
|---|---|
| `SW-LOG-vX.Y.Z-net9-win-x64.zip` | Recommended for new installs |
| `SW-LOG-vX.Y.Z-net6-win-x64.zip` | If your environment is locked to .NET 6 |

---

## Requirements

- Windows 10 / 11
- **.NET 6** *or* **.NET 9** Desktop Runtime
  - [Download .NET 9](https://dotnet.microsoft.com/download/dotnet/9)
  - [Download .NET 6](https://dotnet.microsoft.com/download/dotnet/6) (still works, EOL since Nov 2024)
- Administrator rights only if listening on UDP port 514 (use 5140 otherwise)

---

## Quick Start

1. Run `SyslogViewer.exe`
2. Open **Settings → General** — pick UDP port and log folder
3. Open **Settings → Switches** — map switch IPs to friendly names
4. Configure each switch's WBM to send syslog here (see [Switch setup](#switch-wbm-setup))
5. Messages appear in the live panel and centre log view immediately

The window minimises to the tray on close. Use **tray → Exit** to quit. Only one instance can run at a time (single-instance mutex).

---

## UI Overview

```
┌─────────────────────────────────────────────────────────────────┐
│  Search │ Quick Filters │ Lines: 200 │ Refresh │ ⚙ Settings    │
├──────────┬──────────────────────────────────┬───────────────────┤
│ LOG FILES│ Time      Severity Host  Message │ LIVE EVENTS  [n]  │
│ ▾ events │ 10:23:01  [INFO]  SW-01  Link up │ ─ SWITCHES ─      │
│   SW-01  │ 10:22:58  [WARN]  SW-02  Login   │   SW-01  SW-02    │
│   SW-02  │ ...                              │ ─────────────     │
│          │                                  │ 10:23 SW-01 …     │
└──────────┴──────────────────────────────────┴───────────────────┘
```

| Region | Behaviour |
|---|---|
| **Left sidebar** | Log files grouped by date, expand a date to see per-switch files. |
| **Centre** | Recent N lines from the selected file. Click `▸` to expand the raw packet. |
| **Right (Live)** | Streaming feed with fade-out, plus active switch list. |
| Side panels | Both collapsible. Live strip flashes accent colour on new message when collapsed. |
| Title bar | Coloured to match the active theme on Windows 11 (DWM). |

---

## Themes

Four built-in themes — **Cyber**, **Light**, **Midnight**, **SlateGlass**. Switch in **Settings → Themes**.

Each theme is fully editable: **Edit Colors** opens a per-theme palette editor (17 brushes — backgrounds, accents, text, status, borders, log-row tints). Changes save as a delta against the built-in defaults; **Reset to Defaults** drops the override and saves.

---

## Settings

Stored at `%APPDATA%SyslogViewer\settings.json`. The UI is the only supported way to edit them — a malformed JSON file is backed up to `settings.json.broken-<timestamp>.bak` and replaced with defaults on next start.

### General

| Setting | Description |
|---|---|
| **UDP Port** | Listener port. 514 = standard syslog (admin). 5140 = unprivileged. |
| **Test packet** | Sends a sample syslog datagram to the configured port — quick collector self-check. |
| **Retention (days)** | Files older than this are deleted on startup and daily at midnight. |
| **Log folder** | Where `.log` files go. Restricted: paths under `C:\Windows`, `Program Files`, etc. fall back to `%LocalAppData%SyslogViewer\logs` for safety. |
| **Start with Windows** | Adds/removes the `HKCU\…\Run` autostart entry. |
| **Balloon notifications** | Custom WPF toast on new message; per-label opt-in (only WARN+ by default). |

### Switches

Maps source IP → friendly name. The name appears in the Host column and in log filenames. Unmapped IPs are still collected (raw IP used as the name).

### Event Filters

Regex list. Messages matching **any** pattern are duplicated to `events_YYYY-MM-DD.log`. Empty list = all messages go to events. Patterns run with a 50 ms timeout (ReDoS-safe).

### Message Types

Override the severity chip per regex pattern. First match wins. Useful for highlighting `LINK DOWN` in red, `NTP SYNC` in cyan, etc. Click a colour swatch to open the system colour picker.

### Quick Filters

Toolbar buttons that load a search query in one click.

---

## Log Files

Per switch, per day, plus a daily filtered events file:

```
logs\
├── events_2026-04-25.log                       # filtered events, all switches
├── 192_168_1_10_SW-Panel-A_2026-04-25.log      # named switch
├── 192_168_1_11_2026-04-25.log                 # un-named switch (raw IP)
└── …
```

Each line stores the **full original packet** so the operator can inspect device timestamps, app-name and structured-data via the row-expand view:

```
2026-04-25 10:23:01 [INFO  ] 192.168.1.10 (SW-Panel-A) | <134>1 2026-04-25T10:23:01+00:00 SW-Panel-A 6GK5216-4BS00-2AC2 69 - [meta sysUpTime="14171"] Link up on P0.3
└─ recv timestamp ┘ └─sev─┘ └────── source ──────────┘   └────────── raw RFC 5424 packet ──────────────────────────────────────────────────────────────────────┘
```

The viewer collapses each row to the human payload (`Link up on P0.3`); click `▸` to see the full raw packet.

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

## Reliability & Security

The collector is built to survive a hostile network — UDP is unauthenticated and easily spoofed.

| Threat | Mitigation |
|---|---|
| ReDoS via crafted UDP message against user regex | All user-supplied patterns run with a 50 ms timeout (`SafeRegex`) and are compiled & cached. |
| File-handle exhaustion via spoofed source IPs | LRU cap on open writers (64); least-recently-used are evicted under pressure. |
| UDP flood → disk I/O DOS | Receive thread is decoupled from disk via a bounded `Channel<>` (4096 capacity, drop-on-full). Oversize datagrams (>8 KB) are rejected. |
| Path traversal via malicious `settings.json` | `LogPath` is validated; system folders are refused, fallback is `%LocalAppData%SyslogViewer\logs`. Filenames are sanitised on every write/read. |
| Hyperlink injection in the Host column | Only valid IPv4 strings become clickable links; the navigate handler additionally validates the scheme is `https` and the host parses as an `IPAddress`. |
| Single-instance race | Named mutex with proper release on dispose. |

The UDP listener accepts packets from any source — bind your switches to a dedicated VLAN or use a host firewall if the listener is exposed.

---

## Building

The project multi-targets **.NET 6** and **.NET 9** from a single source tree:

```
dotnet build csharp -c Release
```

Output:
- `csharp\bin\Release\net6.0-windows\SyslogViewer.exe`
- `csharp\bin\Release\net9.0-windows\SyslogViewer.exe`

Build a single TFM:
```
dotnet build csharp -c Release -f net9.0-windows
```

Self-contained publish:
```
dotnet publish csharp -c Release -f net9.0-windows -r win-x64 --self-contained true
```

Requires .NET 9 SDK to build (the SDK can target both net6 and net9).

---

## Project Layout

```
csharp\           ← WPF desktop app — recommended for day-to-day use (GUI)
python\           ← Python collector + HTTP viewer — headless / cross-platform
powershell\       ← PowerShell collector + HTTP viewer — zero-dependency on Windows
logs\             ← runtime output (gitignored)
```

All three implementations write **byte-identical log files** and can share the same `logs\` folder. Pick whichever fits your environment:

| Use the… | …if you need |
|---|---|
| **C# WPF app** | A desktop GUI with live feed, themes, toasts |
| **Python collector** | A headless service, cross-platform, easy to deploy as a systemd unit / scheduled task |
| **PowerShell collector** | Zero install on Windows — runs on built-in PowerShell, no .NET / Python required |

The Python and PowerShell viewers (`viewer.html` in each folder) are static HTML pages — open them in any browser to read the same `logs\` folder.

---

## Author

IVTI
