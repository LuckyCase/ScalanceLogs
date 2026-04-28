"""
Industrial Syslog Collector
=========================
Receives UDP Syslog from Siemens industrial switches, writes per-switch logs
and a separate events.log with filtered events.

Log files are named with the current date, e.g.:
  events_2025-01-15.log
  192_168_1_10_SW-Panel-A_2025-01-15.log

A new file is created automatically each day.
Files older than LOG_ROTATE_DAYS are deleted at startup and daily at midnight.

Run:    python scalance_collector.py
Rights: administrator (for port 514) or change port to 5140 in config.py
"""

import logging
import os
import sys
import re
import socketserver
import threading
from datetime import date, datetime, timedelta

try:
    import config
except ModuleNotFoundError:
    print("ERROR: config.py not found next to the script.")
    sys.exit(1)

LISTEN_HOST    = config.LISTEN_HOST
LISTEN_PORT    = config.LISTEN_PORT
LOG_DIR        = config.LOG_DIR
ROTATE_DAYS    = config.LOG_ROTATE_DAYS
SWITCH_NAMES   = config.SWITCH_NAMES
EVENT_PATTERNS = config.EVENT_PATTERNS

os.makedirs(LOG_DIR, exist_ok=True)

# ─── Logger factory ───────────────────────────────────────────
_loggers: dict[str, logging.Logger] = {}
_loggers_lock = threading.Lock()

def make_logger(key: str, filepath: str) -> logging.Logger:
    with _loggers_lock:
        if key in _loggers:
            return _loggers[key]
        logger = logging.getLogger(key)
        logger.setLevel(logging.DEBUG)
        handler = logging.FileHandler(filepath, encoding="utf-8")
        handler.setFormatter(logging.Formatter("%(message)s"))
        logger.addHandler(handler)
        _loggers[key] = logger
        return logger

def _today() -> str:
    return date.today().strftime("%Y-%m-%d")

def get_events_logger() -> logging.Logger:
    d = _today()
    return make_logger(f"events_{d}", os.path.join(LOG_DIR, f"events_{d}.log"))

def get_host_logger(ip: str, label: str) -> logging.Logger:
    safe_ip = ip.replace(".", "_")
    base = f"{safe_ip}_{label}" if label and label != ip else safe_ip
    d = _today()
    return make_logger(f"host_{base}_{d}", os.path.join(LOG_DIR, f"{base}_{d}.log"))

# ─── Old log cleanup ──────────────────────────────────────────
def cleanup_old_logs():
    cutoff = datetime.now() - timedelta(days=ROTATE_DAYS)
    try:
        for fname in os.listdir(LOG_DIR):
            if not fname.endswith(".log"):
                continue
            fpath = os.path.join(LOG_DIR, fname)
            if datetime.fromtimestamp(os.path.getmtime(fpath)) < cutoff:
                os.remove(fpath)
                print(f"Deleted old log: {fname}")
    except Exception as e:
        print(f"Cleanup error: {e}")

def _schedule_daily_cleanup():
    """Schedules cleanup to run every day at midnight."""
    now = datetime.now()
    next_run = (now + timedelta(days=1)).replace(hour=0, minute=0, second=5, microsecond=0)
    delay = (next_run - now).total_seconds()

    def _run():
        cleanup_old_logs()
        _schedule_daily_cleanup()

    t = threading.Timer(delay, _run)
    t.daemon = True
    t.start()

# ─── Helpers ──────────────────────────────────────────────────
SEVERITY_LABELS = [
    "EMERG", "ALERT", "CRIT", "ERROR",
    "WARN",  "NOTICE","INFO", "DEBUG"
]

def severity_label(code: int) -> str:
    return SEVERITY_LABELS[code] if 0 <= code <= 7 else "UNKNOWN"

def host_label(ip: str) -> str:
    name = SWITCH_NAMES.get(ip)
    return f"{ip} ({name})" if name else ip

def is_event(message: str) -> bool:
    if not EVENT_PATTERNS:
        return True
    return any(p.search(message) for p in EVENT_PATTERNS)

# ─── Syslog parsers (RFC 5424 first, RFC 3164 fallback) ──────
# RFC 5424:  <PRI>VERSION TIMESTAMP HOSTNAME APP-NAME PROCID MSGID [SD] MSG
RFC5424_RE = re.compile(
    r"^<(\d+)>\d+\s+\S+\s+(\S+)\s+\S+\s+\S+\s+\S+\s+(.*)$"
)
# RFC 3164:  <PRI>Mmm DD HH:MM:SS HOSTNAME MSG
RFC3164_RE = re.compile(
    r"^<(\d+)>(\w{3}\s+\d+\s+[\d:]+)\s+([\w.\-]+)\s+(.*)$"
)

def _strip_sd(s: str) -> str:
    """Strip RFC 5424 STRUCTURED-DATA: '-' or one or more '[ID …]' blocks."""
    s = s.lstrip()
    if s == "-":      return ""
    if s.startswith("- "): return s[2:].lstrip()
    while s.startswith("["):
        end = s.find("]")
        if end < 0: break
        s = s[end + 1:].lstrip()
    return s

def parse_syslog(raw: str, src_ip: str) -> dict:
    """
    Returns dict with keys: ts, src_ip, hostname, severity, message.
    `message` is the clean human MSG (header + [SD] stripped) — used for
    event-filter matching. The full raw packet is preserved separately and
    written to the file as-is (so users can see device timestamp, app-name,
    sysUpTime via the viewer's expand row).
    """
    now = datetime.now().strftime("%Y-%m-%d %H:%M:%S")

    # RFC 5424 first
    m5 = RFC5424_RE.match(raw)
    if m5:
        priority, hostname, rest = m5.groups()
        return {
            "ts":       now,
            "src_ip":   src_ip,
            "hostname": hostname,
            "severity": int(priority) & 0x07,
            "message":  _strip_sd(rest),
        }

    # RFC 3164 fallback
    m3 = RFC3164_RE.match(raw)
    if m3:
        priority, _dev_ts, hostname, message = m3.groups()
        return {
            "ts":       now,
            "src_ip":   src_ip,
            "hostname": hostname,
            "severity": int(priority) & 0x07,
            "message":  message,
        }

    # Unknown format
    return {
        "ts":       now,
        "src_ip":   src_ip,
        "hostname": src_ip,
        "severity": 6,
        "message":  raw,
    }

def format_line(p: dict, raw: str) -> str:
    """Writes the full raw packet to disk, but with a normalized prefix."""
    label = host_label(p["src_ip"])
    sev   = severity_label(p["severity"])
    return f"{p['ts']} [{sev:6s}] {label} | {raw}"

# ─── UDP handler ──────────────────────────────────────────────
class SyslogHandler(socketserver.BaseRequestHandler):
    def handle(self):
        raw    = self.request[0].strip().decode("utf-8", errors="replace")
        src_ip = self.client_address[0]

        parsed = parse_syslog(raw, src_ip)
        line   = format_line(parsed, raw)   # full raw packet stored in file

        name = SWITCH_NAMES.get(src_ip, src_ip)
        get_host_logger(src_ip, name).info(line)

        # Event filter runs against the clean payload, not the raw packet,
        # so a pattern like 'meta' won't accidentally match every message.
        if is_event(parsed["message"]):
            get_events_logger().info(line)
            print(line)

# ─── Entry point ──────────────────────────────────────────────
def main():
    mode       = "ALL messages" if not EVENT_PATTERNS else f"{len(EVENT_PATTERNS)} pattern(s)"
    names_info = f"{len(SWITCH_NAMES)} switch(es)" if SWITCH_NAMES else "no names configured (IP only)"

    print("=" * 60)
    print("  Industrial Syslog Collector")
    print("=" * 60)
    print(f"  Port:     UDP {LISTEN_PORT}")
    print(f"  Logs:     {LOG_DIR}")
    print(f"  Filter:   {mode}")
    print(f"  Switches: {names_info}")
    print(f"  Rotate:   keep {ROTATE_DAYS} days")
    print("=" * 60)
    print("  Ctrl+C to stop")
    print()

    cleanup_old_logs()
    _schedule_daily_cleanup()

    try:
        with socketserver.UDPServer((LISTEN_HOST, LISTEN_PORT), SyslogHandler) as srv:
            srv.serve_forever()
    except PermissionError:
        print(f"\nERROR: no permission to bind port {LISTEN_PORT}.")
        print("Run as administrator, or change port to 5140 in config.py")
        sys.exit(1)
    except KeyboardInterrupt:
        print("\nStopped.")

if __name__ == "__main__":
    main()
