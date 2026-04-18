# =============================================================
#  Scalance Syslog Collector - configuration
#  Loaded by scalance_collector.ps1 and viewer_server.ps1
# =============================================================

# --- Network --------------------------------------------------
$PORT = 5140                     # UDP syslog port
                                # if no admin rights - change to 5140
                                # and set the same port in Scalance WBM

# --- Logs -----------------------------------------------------
$SCRIPT_ROOT = Split-Path -Parent $MyInvocation.ScriptName

# Relative to script folder:  Join-Path $SCRIPT_ROOT "logs"
# Absolute path:               "C:\Logs\Scalance"
$LOG_DIR = Join-Path $SCRIPT_ROOT "logs"

$ROTATE_DAYS = 30               # keep log files for N days

# --- Switch names ---------------------------------------------
# Format: "IP" = "Friendly name"
# If empty - only IP address is written to logs.
$SWITCH_NAMES = @{
    "192.168.1.10" = "SW-Panel-A"
    "192.168.1.11" = "SW-Panel-B"
    "192.168.1.12" = "SW-Cabinet-1"
}

# --- Event filters --------------------------------------------
# Messages matching any pattern go to events_YYYY-MM-DD.log.
# Empty array = write ALL messages to events log.
#
# Pattern examples:
#   "(?i)link\s+(up|down)"       - link up / link down
#   "(?i)port\s*\d+.*(up|down)"  - Port 3 link down
#   "(?i)(fault|error|fail)"     - any fault / error / fail
#   "(?i)(restart|reboot)"       - device restart
#   "(?i)snmp.*auth"             - SNMP authentication failure
#
$EVENT_PATTERNS = @()
