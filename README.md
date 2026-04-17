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

## Files

| File | Description |
|------|-------------|
| `config.py` | Configuration — edit this to set up your switches |
| `scalance_collector.py` | UDP syslog listener and log writer |
| `viewer_server.py` | HTTP server for the web UI |
| `viewer.html` | Single-page log viewer UI |

## Author

IVTI
