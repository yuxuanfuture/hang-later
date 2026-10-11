#!/usr/bin/python3
"""Chrome native messaging host. No server, background process or network."""
import base64
import json
import os
from pathlib import Path
import struct
import sys
import time
from urllib.parse import urlsplit
import uuid

MAX_MESSAGE_BYTES = 16 * 1024 * 1024
EXTENSION_ID = "ahkgcekpbdbdkgmkfefnlpacglodpiji"


def read_exact(stream, count):
    result = bytearray()
    while len(result) < count:
        chunk = stream.read(count - len(result))
        if not chunk:
            raise ValueError("Incomplete native message")
        result.extend(chunk)
    return bytes(result)


def receive(stream):
    length = struct.unpack("=I", read_exact(stream, 4))[0]
    if length > MAX_MESSAGE_BYTES:
        raise ValueError("截图太大，请缩小浏览器窗口后重试。")
    return json.loads(read_exact(stream, length).decode("utf-8"))


def validate_capture(message):
    if not isinstance(message, dict):
        raise ValueError("Invalid capture")
    url = message.get("url")
    if not isinstance(url, str) or len(url) > 65536 or any(char.isspace() for char in url):
        raise ValueError("Invalid URL")
    parsed = urlsplit(url)
    if parsed.scheme not in ("http", "https") or not parsed.hostname or parsed.username or parsed.password:
        raise ValueError("Only http and https pages can be saved")
    title = message.get("title", parsed.hostname)
    if not isinstance(title, str):
        raise ValueError("Invalid title")
    result = {"url": url, "title": title[:1000] or parsed.hostname}
    screenshot = message.get("screenshot")
    if screenshot is not None:
        if not isinstance(screenshot, str):
            raise ValueError("Invalid screenshot")
        raw = base64.b64decode(screenshot, validate=True)
        if not raw.startswith(b"\x89PNG\r\n\x1a\n"):
            raise ValueError("Expected a PNG screenshot")
        result["screenshot"] = screenshot
    return result


def save_capture(message, inbox):
    capture = validate_capture(message)
    inbox.mkdir(parents=True, exist_ok=True)
    filename = f"{time.time_ns()}-{uuid.uuid4()}.json"
    temporary = inbox / (filename + ".tmp")
    try:
        with temporary.open("x", encoding="utf-8") as output:
            json.dump(capture, output, ensure_ascii=False)
        os.chmod(temporary, 0o600)
        os.replace(temporary, inbox / filename)
    finally:
        if temporary.exists():
            temporary.unlink()


def main():
    try:
        if len(sys.argv) < 2 or sys.argv[1] != f"chrome-extension://{EXTENSION_ID}/":
            raise ValueError("Unauthorized extension")
        message = receive(sys.stdin.buffer)
        # The application installs this file into HangLater/BrowserBridge.
        inbox = Path(__file__).resolve().parent.parent / "Inbox"
        save_capture(message, inbox)
        response = {"ok": True}
    except Exception as error:
        response = {"ok": False, "error": str(error)}
    payload = json.dumps(response, ensure_ascii=False).encode("utf-8")
    sys.stdout.buffer.write(struct.pack("=I", len(payload)) + payload)
    sys.stdout.buffer.flush()


if __name__ == "__main__":
    main()
