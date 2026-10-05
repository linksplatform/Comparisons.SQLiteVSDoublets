"""Ensure packaging preserves original bytes and detects incomplete evidence."""

import contextlib
import io
import tarfile
import tempfile
import unittest
from pathlib import Path

from index_evidence import index
from package_evidence import ARCHIVE, DIRECTORIES, package, verify


class EvidencePackageTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory()
        self.root = Path(self.temporary.name)
        for name in DIRECTORIES:
            (self.root / name).mkdir()
        (self.root / "ci-logs/result.log").write_bytes(b"\x00\xffsample\n")
        (self.root / "github/issue.json").write_text('{"issue": 110}\n')
        with contextlib.redirect_stdout(io.StringIO()):
            package(self.root)
            index(self.root)

    def tearDown(self):
        self.temporary.cleanup()

    def replace_archive(self, members):
        with tarfile.open(self.root / ARCHIVE, "w:gz") as archive:
            for name, content in members:
                member = tarfile.TarInfo(name)
                member.size = len(content)
                archive.addfile(member, io.BytesIO(content))

    def test_binary_and_text_evidence_round_trip(self):
        with contextlib.redirect_stdout(io.StringIO()):
            verify(self.root)

    def test_unextracted_directory_preserves_existing_archive(self):
        before = (self.root / ARCHIVE).read_bytes()
        (self.root / "templates").rmdir()
        with self.assertRaisesRegex(FileNotFoundError, "Extract"):
            package(self.root)
        self.assertEqual((self.root / ARCHIVE).read_bytes(), before)

    def test_changed_bytes_fail_even_with_original_size(self):
        self.replace_archive([("ci-logs/result.log", b"\x00\xffSample\n")])
        with self.assertRaisesRegex(ValueError, "hash mismatch"):
            verify(self.root)

    def test_missing_members_fail(self):
        self.replace_archive([])
        with self.assertRaisesRegex(ValueError, "Missing evidence members"):
            verify(self.root)

    def test_unexpected_members_fail(self):
        self.replace_archive([("other.log", b"unexpected")])
        with self.assertRaisesRegex(ValueError, "Unexpected evidence member"):
            verify(self.root)

    def test_duplicate_members_fail(self):
        member = ("ci-logs/result.log", b"\x00\xffsample\n")
        self.replace_archive([member, member])
        with self.assertRaisesRegex(ValueError, "Unexpected evidence member"):
            verify(self.root)


if __name__ == "__main__":
    unittest.main()
