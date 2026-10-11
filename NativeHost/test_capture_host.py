import importlib.util
import io
import json
from pathlib import Path
import struct
import tempfile
import unittest

spec = importlib.util.spec_from_file_location("capture_host", Path(__file__).with_name("capture-host.py"))
host = importlib.util.module_from_spec(spec)
spec.loader.exec_module(host)


class CaptureHostTests(unittest.TestCase):
    def test_native_frame_and_atomic_inbox_delivery(self):
        capture = {"title": "视频学习", "url": "https://example.com/watch?t=123"}
        payload = json.dumps(capture).encode()
        message = host.receive(io.BytesIO(struct.pack("=I", len(payload)) + payload))
        with tempfile.TemporaryDirectory() as directory:
            inbox = Path(directory) / "Inbox"
            host.save_capture(message, inbox)
            files = list(inbox.iterdir())
            self.assertEqual(len(files), 1)
            self.assertEqual(files[0].suffix, ".json")
            self.assertEqual(json.loads(files[0].read_text()), capture)

    def test_invalid_input_never_reaches_inbox(self):
        with tempfile.TemporaryDirectory() as directory:
            inbox = Path(directory) / "Inbox"
            for capture in [
                {"title": "bad", "url": "javascript:alert(1)"},
                {"title": "bad", "url": "https://user:pass@example.com"},
                {"title": "bad", "url": "https://example.com", "screenshot": "ZmFrZQ=="},
                {"title": 123, "url": "https://example.com"},
            ]:
                with self.assertRaises(ValueError):
                    host.save_capture(capture, inbox)
            self.assertFalse(inbox.exists())

    def test_short_or_oversized_frame_is_rejected(self):
        for frame in [b"", struct.pack("=I", 100) + b"{}", struct.pack("=I", host.MAX_MESSAGE_BYTES + 1)]:
            with self.assertRaises(ValueError):
                host.receive(io.BytesIO(frame))


if __name__ == "__main__":
    unittest.main()
