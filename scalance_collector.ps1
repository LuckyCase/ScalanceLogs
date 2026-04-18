# =============================================================
#  scalance_collector.ps1
#  Scalance Syslog Collector - PowerShell edition
#  Run:    powershell -ExecutionPolicy Bypass -File scalance_collector.ps1
#  Rights: Administrator required for port 514
#          Alternatively change PORT to 5140 in config.ps1
# =============================================================

. "$PSScriptRoot\config.ps1"

# =============================================================

$SEVERITY_LABELS = @("EMERG","ALERT","CRIT","ERROR","WARN","NOTICE","INFO","DEBUG")

# --- Create log directory if missing --------------------------
New-Item -ItemType Directory -Force -Path $LOG_DIR | Out-Null

# --- Log rotation: remove files older than $ROTATE_DAYS ------
function Invoke-LogRotation {
    $cutoff = (Get-Date).AddDays(-$ROTATE_DAYS)
    Get-ChildItem -Path $LOG_DIR -Filter "*.log" |
        Where-Object { $_.LastWriteTime -lt $cutoff } |
        Remove-Item -Force
}

# --- Build display label for a source IP ----------------------
function Get-HostLabel($ip) {
    if ($SWITCH_NAMES.ContainsKey($ip)) {
        return "$ip ($($SWITCH_NAMES[$ip]))"
    }
    return $ip
}

# --- Resolve dated log file path ------------------------------
function Get-DatedLogPath($baseName) {
    $date = Get-Date -Format "yyyy-MM-dd"
    return Join-Path $LOG_DIR "${baseName}_${date}.log"
}

function Get-HostLogPath($ip) {
    $safeIP = $ip.Replace(".", "_")
    if ($SWITCH_NAMES.ContainsKey($ip)) {
        return Get-DatedLogPath "${safeIP}_$($SWITCH_NAMES[$ip])"
    }
    return Get-DatedLogPath $safeIP
}

function Get-EventsLogPath {
    return Get-DatedLogPath "events"
}

# --- Parse Syslog RFC 3164 ------------------------------------
function Parse-Syslog($raw, $srcIP) {
    $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $result = @{
        Ts       = $ts
        SrcIP    = $srcIP
        Severity = 6
        Message  = $raw
    }
    if ($raw -match '^<(\d+)>\w{3}\s+\d+\s+[\d:]+\s+[\w.\-]+\s+(.*)$') {
        $result.Severity = [int]$Matches[1] -band 0x07
        $result.Message  = $Matches[2]
    }
    return $result
}

# --- Check message against event filter patterns --------------
function Test-IsEvent($message) {
    if ($EVENT_PATTERNS.Count -eq 0) { return $true }
    foreach ($pattern in $EVENT_PATTERNS) {
        if ($message -match $pattern) { return $true }
    }
    return $false
}

# --- Append line to file; run rotation on day change ---------
$script:currentDate = (Get-Date).Date

function Write-LogLine($filepath, $line) {
    $today = (Get-Date).Date
    if ($today -ne $script:currentDate) {
        $script:currentDate = $today
        Invoke-LogRotation
    }
    $stream = [System.IO.File]::Open(
        $filepath,
        [System.IO.FileMode]::Append,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::ReadWrite
    )
    $writer = [System.IO.StreamWriter]::new($stream, [System.Text.Encoding]::UTF8)
    try   { $writer.WriteLine($line) }
    finally { $writer.Close(); $stream.Close() }
}

# --- Main loop ------------------------------------------------
$endpoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any, $PORT)
try {
    $udpClient = New-Object System.Net.Sockets.UdpClient($PORT)
} catch {
    Write-Host ""
    Write-Host "ERROR: failed to bind port $PORT" -ForegroundColor Red
    Write-Host "Run as Administrator, or change PORT to 5140 in config.ps1." -ForegroundColor Yellow
    exit 1
}

Invoke-LogRotation

Write-Host ("=" * 60)
Write-Host "  Scalance Syslog Collector (PowerShell)"
Write-Host ("=" * 60)
Write-Host "  Port:     UDP $PORT"
Write-Host "  Logs:     $LOG_DIR"
$filterInfo = if ($EVENT_PATTERNS.Count -eq 0) { "ALL messages" } else { "$($EVENT_PATTERNS.Count) pattern(s)" }
Write-Host "  Filter:   $filterInfo"
$namesInfo  = if ($SWITCH_NAMES.Count -eq 0) { "no names defined (IP only)" } else { "$($SWITCH_NAMES.Count) switch(es)" }
Write-Host "  Switches: $namesInfo"
Write-Host "  Rotate:   keep $ROTATE_DAYS days"
Write-Host ("=" * 60)
Write-Host "  Press Ctrl+C to stop"
Write-Host ""

try {
    while ($true) {
        $data  = $udpClient.Receive([ref]$endpoint)
        $srcIP = $endpoint.Address.ToString()
        $raw   = [System.Text.Encoding]::UTF8.GetString($data).Trim()

        # IP whitelist — drop packets from unknown sources
        if ($SWITCH_NAMES.Count -gt 0 -and -not $SWITCH_NAMES.ContainsKey($srcIP)) {
            $ts = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
            $unknownLog = Join-Path $LOG_DIR "unknown_sources.log"
            $unknownLine = "$ts [WARN  ] REJECTED $srcIP | $raw"
            $stream = [System.IO.File]::Open($unknownLog, [System.IO.FileMode]::Append, [System.IO.FileAccess]::Write, [System.IO.FileShare]::ReadWrite)
            $writer = [System.IO.StreamWriter]::new($stream, [System.Text.Encoding]::UTF8)
            try   { $writer.WriteLine($unknownLine) }
            finally { $writer.Close(); $stream.Close() }
            Write-Host $unknownLine -ForegroundColor DarkGray
            continue
        }

        $parsed = Parse-Syslog $raw $srcIP
        $label  = Get-HostLabel $srcIP
        $sevStr = $SEVERITY_LABELS[$parsed.Severity]
        $line   = "$($parsed.Ts) [$("{0,-6}" -f $sevStr)] $label | $($parsed.Message)"

        Write-LogLine (Get-HostLogPath $srcIP) $line

        if (Test-IsEvent $parsed.Message) {
            Write-LogLine (Get-EventsLogPath) $line

            $color = switch ($parsed.Severity) {
                { $_ -le 3 } { "Red" }
                4            { "Yellow" }
                default      { "Cyan" }
            }
            if ($parsed.Message -match "(?i)link\s+up|port.*\bup\b")    { $color = "Green" }
            if ($parsed.Message -match "(?i)link\s+down|port.*\bdown\b") { $color = "Red" }

            Write-Host $line -ForegroundColor $color
        }
    }
} finally {
    $udpClient.Close()
    Write-Host "Stopped."
}
