# Industrial Syslog Collector

Collects UDP Syslog from Siemens Scalance XC-200 switches, writes per-switch log files, and provides a browser-based log viewer.

## Overview

```
Scalance XC-200  ──UDP 514──►  collector  ──►  logs/
                                                  │
Browser  ◄──HTTP 8080──  viewer_server  ◄─────────┘
```

Two independent processes:
- **Collector** — listens for syslog UDP packets, writes log files
- **Viewer** — HTTP server serving the web UI and log API

Available in two implementations — choose either, they are fully compatible and share the same `logs/` folder and `viewer.html`.

## Project Structure

```
ScalanceLogs/
├── python/
│   ├── config.py                  # Python configuration
│   ├── scalance_collector.py      # UDP syslog collector
│   └── viewer_server.py           # HTTP log viewer server
├── powershell/
│   ├── config.ps1                 # PowerShell configuration
│   ├── scalance_collector.ps1     # UDP syslog collector
│   └── viewer_server.ps1          # HTTP log viewer server
├── viewer.html                    # Web UI (shared by both implementations)
└── logs/                          # Log files (shared by both implementations)
```

---

## Python

### Requirements

- Python 3.9+
- Administrator rights (for UDP port 514)

### Quick Start

**1. Configure** — edit `python/config.py`:

```python
SWITCH_NAMES = {
    "192.168.1.10": "SW-Panel-A",
    "192.168.1.11": "SW-Panel-B",
}
```

**2. Start collector** (as Administrator)

```
python python/scalance_collector.py
```

**3. Start viewer** (separate window)

```
python python/viewer_server.py
```

---

## PowerShell

### Requirements

- PowerShell 5.1+
- Administrator rights (for UDP port 514)

### Quick Start

**1. Configure** — edit `powershell/config.ps1`:

```powershell
$SWITCH_NAMES = @{
    "192.168.1.10" = "SW-Panel-A"
    "192.168.1.11" = "SW-Panel-B"
}
```

**2. Start collector** (as Administrator)

```
powershell -ExecutionPolicy Bypass -File powershell/scalance_collector.ps1
```

**3. Start viewer** (separate window)

```
powershell -ExecutionPolicy Bypass -File powershell/viewer_server.ps1
```

---

## Open the Viewer

```
http://localhost:8080
```

---

## Configuration

### Switch Names

Maps source IP addresses to friendly names shown in the viewer and written to log files.

```python
# Python (config.py)
SWITCH_NAMES = {
    "192.168.1.10": "SW-Panel-A",
    "192.168.1.11": "SW-Panel-B",
}
```

```powershell
# PowerShell (config.ps1)
$SWITCH_NAMES = @{
    "192.168.1.10" = "SW-Panel-A"
    "192.168.1.11" = "SW-Panel-B"
}
```

If empty — only IP addresses are written to logs. All sources are accepted.

### Event Filters

Messages matching any pattern are written to `events_YYYY-MM-DD.log`. The events log is the default view in the web UI.

```python
# Python (config.py)
EVENT_PATTERNS = [
    re.compile(r"(?i)link\s+(up|down)"),
    re.compile(r"(?i)(fault|error|fail)"),
]
# Empty list = write ALL messages to events log
```

```powershell
# PowerShell (config.ps1)
$EVENT_PATTERNS = @(
    "(?i)link\s+(up|down)",
    "(?i)(fault|error|fail)"
)
# Empty array = write ALL messages to events log
```

### Message Type Overrides *(Python only)*

Custom labels and colors for the severity chip in the viewer, matched against the message text:

```python
# Python (config.py)
MESSAGE_TYPES = [
    {
        "pattern": re.compile(r"(?i)link\s+down"),
        "label":   "LINK DOWN",
        "color":   "#ff6b6b",
        "bg":      "rgba(255,107,107,0.15)",
    },
    ...
]
```

### Quick Filters *(Python only)*

Shortcut buttons shown in the viewer toolbar:

```python
# Python (config.py)
QUICK_FILTERS = [
    {"label": "Link",   "query": "link"},
    {"label": "NTP",    "query": "ntp"},
]
```

### Log Directory

```python
# Python — relative to project root by default
LOG_DIR = str(_ROOT / "logs")
# or absolute:
LOG_DIR = r"C:\Logs\SW-Log"
```

```powershell
# PowerShell — relative to project root by default
$LOG_DIR = Join-Path $SCRIPT_ROOT "logs"
# or absolute:
$LOG_DIR = "C:\Logs\SW-Log"
```

### Port 514

Requires Administrator. To run without elevated rights change to `5140` in config and in the switch WBM → System → Syslog → Port.

---

## Log Files

Files are created daily in `logs/`:

```
logs/
├── events_2026-04-18.log                        # filtered events, all switches
├── 192_168_1_10_SW-Panel-A_2026-04-18.log       # all messages, per switch
├── 192_168_1_11_SW-Panel-B_2026-04-18.log
└── unknown_sources.log                          # rejected packets from unlisted IPs
```

Log line format:
```
2026-04-18 10:23:01 [INFO  ] 192.168.1.10 (SW-Panel-A) | <134>1 2000-01-08T09:23:01+00:00 ... Link up on P0.3.
```

Files older than `LOG_ROTATE_DAYS` (default: 30) are deleted automatically at startup and daily at midnight.

---

## Viewer Features

- **Log files panel** — grouped by date; events file is the default, individual switch files expandable under it
- **Switches panel** — live indicator of which switches are sending messages; entries appear, fade, and disappear
- **Live events panel** — new messages appear with highlight animation as they arrive
- **Message detail** — click `▸` on any row to expand the full raw syslog line
- **Quick filters** — one-click filter buttons defined in `config.py`
- **Search** — filter by any text (Enter or Refresh)
- **Auto-refresh** — polls every 5 seconds when enabled
- **Collapsible panels** — left sidebar and right live panel can be collapsed to save space
- **Status indicator** — shows time since last new event; turns yellow if stale

---

## Switch WBM Setup

```
System → Time → SNTP Client
  Server Address: <IP of this PC>

System → Syslog
  Server Address: <IP of this PC>
  Port:           514  (or 5140 if not running as Administrator)
  Severity:       Informational (or higher)
```

---

## Security

### IP Whitelist

The collector accepts syslog packets **only from IPs listed in `SWITCH_NAMES`**. Packets from unlisted sources are rejected and logged to `logs/unknown_sources.log`.

If `SWITCH_NAMES` is empty — all sources are accepted (useful during initial setup).

### XSS Protection

All syslog message content is HTML-escaped before rendering. Injected HTML from malicious packets is displayed as plain text and never executed.

### Network Exposure

- **Collector** (UDP 514/5140) — accessible from the local network. Only listed IPs are accepted.
- **Viewer** (HTTP 8080) — binds to `localhost` only by default. Not reachable from the network.

---

## Author

IVTI
