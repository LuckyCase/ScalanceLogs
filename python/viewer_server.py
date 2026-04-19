"""
SW-Log Viewer — HTTP server
==================================
Serves viewer.html and an API for reading log files.

Run:  python viewer_server.py
Open: http://localhost:8080
"""

import http.server
import json
import os
import sys
import urllib.parse
from pathlib import Path

try:
    import config
except ModuleNotFoundError:
    print("ERROR: config.py not found next to the script.")
    sys.exit(1)

LOG_DIR    = config.LOG_DIR
PORT       = 8080
SCRIPT_DIR = Path(__file__).parent


class Handler(http.server.BaseHTTPRequestHandler):

    def log_message(self, format, *args):
        pass  # suppress default access log output

    def do_GET(self):
        parsed = urllib.parse.urlparse(self.path)
        path   = parsed.path
        params = urllib.parse.parse_qs(parsed.query)

        if path == "/" or path == "/index.html":
            self._serve_file(SCRIPT_DIR / "viewer.html", "text/html")

        elif path == "/api/files":
            self._json(self._list_logs())

        elif path == "/api/log":
            filename = params.get("file", ["events.log"])[0]
            lines    = int(params.get("lines", [200])[0])
            search   = params.get("search", [""])[0].lower()
            self._json(self._read_log(filename, lines, search))

        elif path == "/api/config":
            self._json(self._get_config())

        else:
            self.send_error(404)

    def _serve_file(self, filepath: Path, mime: str):
        if not filepath.exists():
            self.send_error(404, f"File not found: {filepath}")
            return
        data = filepath.read_bytes()
        self.send_response(200)
        self.send_header("Content-Type", mime + "; charset=utf-8")
        self.send_header("Content-Length", len(data))
        self.end_headers()
        self.wfile.write(data)

    def _json(self, obj):
        data = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(200)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", len(data))
        self.end_headers()
        self.wfile.write(data)

    def _list_logs(self) -> list:
        try:
            return sorted(f for f in os.listdir(LOG_DIR) if f.endswith(".log"))
        except Exception:
            return []

    def _read_log(self, filename: str, lines: int, search: str) -> dict:
        safe     = os.path.basename(filename)
        filepath = os.path.join(LOG_DIR, safe)
        try:
            with open(filepath, "r", encoding="utf-8", errors="replace") as f:
                all_lines = f.readlines()

            if search:
                all_lines = [l for l in all_lines if search in l.lower()]

            result = [l.rstrip() for l in all_lines[-lines:]]
            return {"lines": result, "total": len(all_lines), "file": safe}
        except FileNotFoundError:
            return {"lines": [], "total": 0, "file": safe, "error": "File not found"}
        except Exception as e:
            return {"lines": [], "total": 0, "file": safe, "error": str(e)}

    def _get_config(self) -> dict:
        result = {"message_types": [], "quick_filters": []}

        if hasattr(config, "MESSAGE_TYPES"):
            for mt in config.MESSAGE_TYPES:
                result["message_types"].append({
                    "pattern": mt["pattern"].pattern,
                    "label":   mt["label"],
                    "color":   mt.get("color", ""),
                    "bg":      mt.get("bg", ""),
                })

        if hasattr(config, "QUICK_FILTERS"):
            result["quick_filters"] = list(config.QUICK_FILTERS)

        return result


def main():
    print(f"SW-Log Viewer  →  http://localhost:{PORT}")
    print(f"Logs from: {LOG_DIR}")
    print("Ctrl+C to stop")
    with http.server.HTTPServer(("0.0.0.0", PORT), Handler) as srv:
        try:
            srv.serve_forever()
        except KeyboardInterrupt:
            print("\nStopped.")

if __name__ == "__main__":
    main()
