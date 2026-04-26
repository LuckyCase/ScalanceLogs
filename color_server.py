from http.server import HTTPServer, SimpleHTTPRequestHandler
import os

os.chdir(r"C:\Projects\ScalanceLogs")

server = HTTPServer(("localhost", 7788), SimpleHTTPRequestHandler)
server.serve_forever()
