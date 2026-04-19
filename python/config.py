# =============================================================
#  Industrial Syslog Collector — configuration
# =============================================================

import re

# ─── Network ──────────────────────────────────────────────────
LISTEN_HOST = "0.0.0.0"   # listen on all interfaces
LISTEN_PORT = 514          # standard Syslog UDP port
                           # if no admin rights — change to 5140
                           # and set the same port in the switch WBM

# ─── Logs ─────────────────────────────────────────────────────
from pathlib import Path
_HERE = Path(__file__).parent        # python/
_ROOT = _HERE.parent                 # project root (ScalanceLogs/)

# Relative to project root:  str(_ROOT / "logs")
# Absolute path:              r"C:\Logs\SW-Log"
LOG_DIR = str(_ROOT / "logs")

LOG_ROTATE_DAYS = 30   # keep logs for N days

# ─── Switch names ─────────────────────────────────────────────
# Format: "IP-address": "Friendly name"
# If the dict is empty — only the source IP is written to logs.

SWITCH_NAMES = {
    "192.168.1.10": "SW-Panel-A",
    "192.168.1.11": "SW-Panel-B",
    "192.168.1.12": "SW-Cabinet-1",
}

# ─── Event filters ────────────────────────────────────────────
# List of regular expressions. If a message matches at least one
# pattern — it is written to events.log.
# If the list is EMPTY — ALL incoming messages go to events.log.

EVENT_PATTERNS = [
    re.compile(r"(?i)link\s+(up|down)"),
    re.compile(r"(?i)port.*(up|down)"),
    re.compile(r"(?i)(fault|error|fail)"),
]

# ─── Message type overrides ────────────────────────────────────
# Matched against the extracted message text (the useful part after
# syslog structured-data block). First matching pattern wins.
# color/bg override the default severity chip style in the viewer.
#
# Fields:
#   pattern  — compiled regex (matched case-insensitively)
#   label    — text shown in the severity chip instead of INFO/WARN/…
#   color    — chip text color (CSS value)
#   bg       — chip and row background (CSS value, use rgba for transparency)

MESSAGE_TYPES = [
    {
        "pattern": re.compile(r"(?i)link\s+down"),
        "label":   "LINK DOWN",
        "color":   "#ff6b6b",
        "bg":      "rgba(255,107,107,0.15)",
    },
    {
        "pattern": re.compile(r"(?i)link\s+up"),
        "label":   "LINK UP",
        "color":   "#4a9e72",
        "bg":      "rgba(74,158,114,0.12)",
    },
    {
        "pattern": re.compile(r"(?i)time synchronized"),
        "label":   "NTP SYNC",
        "color":   "#4fa8c5",
        "bg":      "rgba(79,168,197,0.10)",
    },
    {
        "pattern": re.compile(r"(?i)time not synchronized"),
        "label":   "NTP LOST",
        "color":   "#b8913a",
        "bg":      "rgba(184,145,58,0.12)",
    },
    {
        "pattern": re.compile(r"(?i)configuration changed"),
        "label":   "CFG CHG",
        "color":   "#b8913a",
        "bg":      "rgba(184,145,58,0.10)",
    },
    {
        "pattern": re.compile(r"(?i)logged in"),
        "label":   "LOGIN",
        "color":   "#a8b4cc",
        "bg":      "rgba(168,180,204,0.08)",
    },
    {
        "pattern": re.compile(r"(?i)logged out|inactivity"),
        "label":   "LOGOUT",
        "color":   "#556070",
        "bg":      "rgba(85,96,112,0.10)",
    },
    {
        "pattern": re.compile(r"(?i)log file cleared"),
        "label":   "LOG CLR",
        "color":   "#556070",
        "bg":      "rgba(85,96,112,0.08)",
    },
]

# ─── Quick filters ─────────────────────────────────────────────
# Shown as clickable buttons in the toolbar.
# Clicking a button fills the search field and reloads the log.
# Clicking again clears the filter.

QUICK_FILTERS = [
    {"label": "Link",    "query": "link"},
    {"label": "NTP",     "query": "ntp"},
    {"label": "Admin",   "query": "admin"},
    {"label": "Config",  "query": "configuration"},
]
