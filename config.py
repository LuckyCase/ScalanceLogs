# =============================================================
#  Scalance Syslog Collector — configuration
# =============================================================

# ─── Network ──────────────────────────────────────────────────
LISTEN_HOST = "0.0.0.0"   # listen on all interfaces
LISTEN_PORT = 514          # standard Syslog UDP port
                           # if no admin rights — change to 5140
                           # and set the same port in Scalance WBM

# ─── Logs ─────────────────────────────────────────────────────
from pathlib import Path
_HERE = Path(__file__).parent   # folder where config.py lives

# Relative to script folder:  str(_HERE / "logs")
# Absolute path:               r"C:\Logs\Scalance"
LOG_DIR = str(_HERE / "logs")

LOG_ROTATE_DAYS = 30   # keep logs for N days

# ─── Switch names ─────────────────────────────────────────────
# Format: "IP-address": "Friendly name"
# If the dict is empty — only the source IP is written to logs.
# If the IP is in the dict — name is appended: "192.168.1.10 (SW-Panel-A)"

SWITCH_NAMES = {
    "192.168.1.10": "SW-Panel-A",
    "192.168.1.11": "SW-Panel-B",
    "192.168.1.12": "SW-Cabinet-1",
}

# Example with empty dict (write IP only):
# SWITCH_NAMES = {}

# ─── Event filters ────────────────────────────────────────────
# List of regular expressions. If a message matches at least one
# pattern — it is written to events.log.
#
# If the list is EMPTY — ALL incoming messages go to events.log.
#
# Pattern syntax (Python re):
#   (?i)         — case-insensitive match (recommended)
#   link\s+down  — "link", whitespace, "down"
#   port.*down   — "port", any chars, "down"
#   (a|b)        — "a" or "b"
#   \d+          — one or more digits
#
# ── Pattern examples ───────────────────────────────────────────
#
# import re
#
# # Link up/down on any port:
# re.compile(r"(?i)link\s+(up|down)"),
#
# # Port N link up/down (with port number):
# re.compile(r"(?i)port\s*\d+.*(up|down)"),
#
# # Any fault / error / fail:
# re.compile(r"(?i)(fault|error|fail)"),
#
# # Specific port (e.g. port 3):
# re.compile(r"(?i)port\s*3\s+link"),
#
# # SNMP authentication failure:
# re.compile(r"(?i)snmp.*auth"),
#
# # Device restart:
# re.compile(r"(?i)(restart|reboot|cold start)"),
#
# ──────────────────────────────────────────────────────────────

import re

EVENT_PATTERNS = [
    re.compile(r"(?i)link\s+(up|down)"),
    re.compile(r"(?i)port.*(up|down)"),
    re.compile(r"(?i)(fault|error|fail)"),
]

# Example with empty list (write all messages):
# EVENT_PATTERNS = []
