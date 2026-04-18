# Scalance Syslog Collector

Collects UDP Syslog from Siemens Scalance XC-200 switches, writes per-switch log files, and provides a browser-based log viewer.

## Overview

```
Scalance XC-200  ──UDP 514──►  scalance_collector.py  ──►  logs/
                                                               │
Browser  ◄──HTTP 8080──  viewer_server.py  ◄───────────────────┘
```

Two independent processes:
- **Collector** — listens for syslog UDP packets, writes log files
- **Viewer** — HTTP server serving the web UI and log API

## Requirements

- Python 3.9+
- Administrator rights (for UDP port 514)

## Quick Start

**1. Configure switches and log path**

Edit `config.py`:

```python
SWITCH_NAMES = {
    "192.168.1.10": "SW-Panel-A",
    "192.168.1.11": "SW-Panel-B",
}
```

**2. Start the collector** (as Administrator)

```
python scalance_collector.py
```

**3. Start the viewer** (separate window)

```
python viewer_server.py
```

**4. Open the viewer**

```
http://localhost:8080
```

## Configuration

All settings are in `config.py`:

| Parameter | Default | Description |
|-----------|---------|-------------|
| `LISTEN_HOST` | `0.0.0.0` | Interface to listen on |
| `LISTEN_PORT` | `514` | UDP syslog port |
| `LOG_DIR` | `./logs` | Log files directory |
| `LOG_ROTATE_DAYS` | `30` | Days to keep log files |
| `SWITCH_NAMES` | `{}` | IP → friendly name mapping |
| `EVENT_PATTERNS` | see below | Regex filters for events.log |

**Log directory** — relative or absolute path:

```python
LOG_DIR = str(_HERE / "logs")        # relative to script folder
LOG_DIR = r"C:\Logs\Scalance"        # absolute path
```

**Event filters** — messages matching any pattern go to `events_YYYY-MM-DD.log`:

```python
EVENT_PATTERNS = [
    re.compile(r"(?i)link\s+(up|down)"),
    re.compile(r"(?i)(fault|error|fail)"),
]
# Empty list = write all messages to events log
```

**Port 514** requires Administrator. To run without elevated rights, change to port 5140 in `config.py` and in Scalance WBM.

## Adding Event Patterns

Scalance switches send different message types depending on firmware and configuration. To discover what your switches actually emit, follow these steps.

**Step 1 — collect everything first**

Set an empty filter so all messages are written to `events.log`:

```powershell
# config.ps1
$EVENT_PATTERNS = @()
```

Run the collector for a day or two during normal operation.

**Step 2 — find unique message types**

```powershell
Get-Content logs\events_2026-04-17.log |
    ForEach-Object { ($_ -split '\|')[-1].Trim() } |
    Sort-Object -Unique
```

Example output:
```
FAULT: Power supply failure
LINK: Port 1 link down
LINK: Port 3 link up
SNMP: Authentication failure from 10.0.0.5
STP: Topology change detected
```

**Step 3 — write patterns from broad to specific**

```powershell
$EVENT_PATTERNS = @(
    "(?i)link\s+(up|down)",    # all link state changes
    "(?i)(fault|error|fail)",  # any fault or error
    "(?i)snmp.*auth",          # SNMP authentication failures
    "(?i)stp",                 # spanning tree events
)
```

**Wildcard in regex** — use `.*` (any characters), not `*`:

```powershell
"(?i)port.*link"   # matches: Port 1 link down, Port 12 link up, ...
"(?i)fault.*"      # matches: fault and anything after it
```

`(?i)` at the start makes the pattern case-insensitive.

## Log Files

Files are created daily and stored in `LOG_DIR`:

```
logs/
├── events_2025-01-15.log          # filtered events from all switches
├── events_2025-01-16.log
├── 192_168_1_10_SW-Panel-A_2025-01-15.log   # all messages per switch
├── 192_168_1_11_SW-Panel-B_2025-01-15.log
└── ...
```

Log line format:
```
2025-01-15 10:23:01 [WARN  ] 192.168.1.10 (SW-Panel-A) | Port 4 link down
```

Files older than `LOG_ROTATE_DAYS` are deleted automatically at startup and daily at midnight.

## Scalance WBM Setup

In the switch Web Based Management:

```
System → Time → SNTP Client
  Server Address: <IP of this PC>

System → Syslog
  Server Address: <IP of this PC>
  Port: 514
  Severity: Informational (or higher)
```

## Viewer Features

- **Sidebar** — list of all log files, grouped by date
- **Search** — filter by any text (press Enter or click Refresh)
- **Auto-refresh** — polls every 5 seconds when enabled
- **Live panel** — real-time event feed with fade highlight on new events
- **Status indicator** — shows time since last new event; turns yellow if stale

## Security

### IP Whitelist

The collector accepts syslog packets **only from IPs listed in `SWITCH_NAMES`** (`config.py` / `config.ps1`). Packets from unknown sources are rejected and written to `logs/unknown_sources.log` for audit.

If `SWITCH_NAMES` is empty — all sources are accepted (development mode).

Rejected packet example in `unknown_sources.log`:
```
2026-04-17 21:14:03 [WARN  ] REJECTED 10.0.0.99 | <134>Apr 17 21:14:03 ...
```

### XSS Protection

All syslog message content is HTML-escaped before rendering in the browser. Injected HTML tags from malicious syslog packets are displayed as plain text and never executed.

### Network Exposure

- **Collector** (UDP 514/5140) — accessible from the local network. Only listed switch IPs are accepted.
- **Viewer** (HTTP 8080) — binds to `localhost` only. Not reachable from the network by default.

### Recommendations

- Keep `SWITCH_NAMES` populated with all known switch IPs
- Run the collector on a dedicated isolated PC or VLAN
- Monitor `unknown_sources.log` for unexpected syslog sources

## Files

| File | Description |
|------|-------------|
| `config.py` | Python configuration |
| `config.ps1` | PowerShell configuration (shared by both PS1 scripts) |
| `scalance_collector.py` | Python UDP syslog collector |
| `scalance_collector.ps1` | PowerShell UDP syslog collector |
| `viewer_server.py` | Python HTTP server for the web UI |
| `viewer_server.ps1` | PowerShell HTTP server for the web UI |
| `viewer.html` | Single-page log viewer UI (used by both server variants) |

**Runtime log files** (in `logs/`):

| File | Description |
|------|-------------|
| `events_YYYY-MM-DD.log` | Filtered events from all switches |
| `192_168_x_x_Name_YYYY-MM-DD.log` | All messages per switch |
| `viewer_server.log` | HTTP server start/stop and errors |
| `unknown_sources.log` | Rejected packets from unlisted IPs |

## Author

IVTI
