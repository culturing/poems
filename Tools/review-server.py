#!/usr/bin/env python3
"""Rating editor for the manual review pass.

Live Server hosts docs/ and cannot write anything; this sidecar does the writing, editing
the source file under Poems/ directly on behalf of the widget in Scripts/review.js.

    dotnet run -- review        # build with the widget, and write the manifest
    python Tools/review-server.py
"""

import json
import os
import re
import sys
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

PORT = 5501
ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MANIFEST = os.path.join(ROOT, "Output", "review-map.json")
PROGRESS = os.path.join(ROOT, "Tools", "review-progress.json")

# Only paths the manifest names are writable, so a stray request cannot reach a file
# outside Poems/.
_manifest = {"poems": [], "max": 2}
_by_url = {}
_mtime = 0.0


def load_manifest(force=False):
    """Reload when the build has rewritten the manifest, so a rebuild mid-session lands."""
    global _manifest, _by_url, _mtime
    try:
        mtime = os.path.getmtime(MANIFEST)
    except OSError:
        raise RuntimeError(
            "Output/review-map.json is missing. Build in review mode first:\n"
            "    dotnet run -- review"
        )
    if force or mtime != _mtime:
        with open(MANIFEST, encoding="utf-8") as fh:
            _manifest = json.load(fh)
        _by_url = {p["url"]: p for p in _manifest["poems"]}
        _mtime = mtime
        print(f"  manifest: {len(_by_url)} poems")
    return _manifest


def load_progress():
    try:
        with open(PROGRESS, encoding="utf-8") as fh:
            return json.load(fh)
    except (OSError, ValueError):
        return {}


def save_progress(progress):
    tmp = PROGRESS + ".tmp"
    with open(tmp, "w", encoding="utf-8") as fh:
        json.dump(progress, fh, indent=0, sort_keys=True)
    os.replace(tmp, PROGRESS)


def read_rating(src):
    """Count the leading asterisks, past a BOM if the file happens to carry one."""
    with open(os.path.join(ROOT, src), "rb") as fh:
        raw = fh.read(16)
    raw = raw[3:] if raw.startswith(b"\xef\xbb\xbf") else raw
    return len(raw) - len(raw.lstrip(b"*"))


def write_rating(src, rating):
    """Rewrite only the leading asterisks. Byte-level, so the file's encoding, BOM (or
    absence of one) and line endings all survive exactly as they were."""
    path = os.path.join(ROOT, src)
    with open(path, "rb") as fh:
        raw = fh.read()
    bom = b"\xef\xbb\xbf" if raw.startswith(b"\xef\xbb\xbf") else b""
    body = raw[len(bom):]
    new = bom + (b"*" * rating) + body.lstrip(b"*")
    if new == raw:
        return False
    tmp = path + ".tmp"
    with open(tmp, "wb") as fh:
        fh.write(new)
    os.replace(tmp, path)
    return True


def poem_state(url):
    load_manifest()
    poem = _by_url.get(url)
    if poem is None:
        return None
    progress = load_progress()
    order = _manifest["poems"]
    nxt = next((p["url"] for p in order[poem["i"] + 1:] if p["url"] not in progress), None)
    if nxt is None:  # wrap, so the tail of the collection does not dead-end
        nxt = next((p["url"] for p in order if p["url"] not in progress), None)
    return {
        "url": url,
        "title": poem["title"],
        "rating": read_rating(poem["src"]),
        "max": _manifest["max"],
        "index": poem["i"] + 1,
        "total": len(order),
        "reviewed": url in progress,
        "reviewedCount": len(progress),
        "nextUnreviewed": nxt,
        "src": poem["src"],
    }


class Handler(BaseHTTPRequestHandler):
    protocol_version = "HTTP/1.1"

    def log_message(self, fmt, *args):  # the default logger is far too chatty here
        pass

    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")
        self.send_header("Access-Control-Allow-Methods", "GET, POST, OPTIONS")

    def _send(self, code, payload):
        body = json.dumps(payload).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self._cors()
        self.end_headers()
        self.wfile.write(body)

    def do_OPTIONS(self):
        self.send_response(204)
        self.send_header("Content-Length", "0")
        self._cors()
        self.end_headers()

    def do_GET(self):
        parsed = urlparse(self.path)
        if parsed.path != "/api/poem":
            return self._send(404, {"error": "not found"})
        url = (parse_qs(parsed.query).get("url") or [""])[0]
        try:
            state = poem_state(url)
        except RuntimeError as exc:
            return self._send(503, {"error": str(exc)})
        if state is None:
            return self._send(404, {"error": f"no poem at {url}"})
        return self._send(200, state)

    def do_POST(self):
        if urlparse(self.path).path != "/api/rating":
            return self._send(404, {"error": "not found"})
        try:
            length = int(self.headers.get("Content-Length") or 0)
            payload = json.loads(self.rfile.read(length) or b"{}")
        except ValueError:
            return self._send(400, {"error": "malformed json"})

        url = payload.get("url")
        try:
            rating = int(payload.get("rating"))
        except (TypeError, ValueError):
            return self._send(400, {"error": "rating must be a number"})

        try:
            load_manifest()
        except RuntimeError as exc:
            return self._send(503, {"error": str(exc)})

        poem = _by_url.get(url)
        if poem is None:
            return self._send(404, {"error": f"no poem at {url}"})
        if not 0 <= rating <= _manifest["max"]:
            return self._send(400, {"error": f"rating must be 0..{_manifest['max']}"})

        changed = write_rating(poem["src"], rating)
        progress = load_progress()
        progress[url] = rating
        save_progress(progress)

        state = poem_state(url)
        state["changed"] = changed
        mark = "*" * rating or "-"
        print(f"  {mark:<3} {poem['title']}" + ("" if changed else "   (unchanged)"))
        return self._send(200, state)


def main():
    try:
        load_manifest(force=True)
    except RuntimeError as exc:
        print(exc)
        return 1
    counts = {}
    for poem in _manifest["poems"]:
        counts[poem["rating"]] = counts.get(poem["rating"], 0) + 1
    spread = "  ".join(f"{'*' * r or '-'}: {n}" for r, n in sorted(counts.items()))
    print(f"review server on http://127.0.0.1:{PORT}")
    print(f"  {spread}")
    print(f"  {len(load_progress())} already reviewed this pass")
    print("  ctrl-c to stop\n")
    try:
        ThreadingHTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
    except KeyboardInterrupt:
        print("\nstopped")
    return 0


if __name__ == "__main__":
    sys.exit(main())
