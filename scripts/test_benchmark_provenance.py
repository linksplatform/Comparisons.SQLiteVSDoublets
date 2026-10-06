"""Snapshot measured software versions rather than attributing today's versions to old runs."""

import json
import tempfile
import unittest
from pathlib import Path

import benchmark_provenance as provenance


class SoftwareTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.root = Path(temporary.name)
        (self.root / "rust").mkdir()
        (self.root / "rust/Cargo.lock").write_text(
            'version = 4\n[[package]]\nname = "doublets"\nversion = "0.5.0"\n'
        )
        (self.root / "csharp/SQLiteVSDoublets").mkdir(parents=True)
        (self.root / "csharp/SQLiteVSDoublets/SQLiteVSDoublets.csproj").write_text(
            '<Project><ItemGroup><PackageReference Version="0.18.1" '
            'Include="Platform.Data.Doublets" /></ItemGroup></Project>'
        )

    def test_reads_resolved_rust_and_csharp_library_versions(self):
        self.assertEqual(provenance.library_versions("Rust", self.root), {"doublets": "0.5.0"})
        self.assertEqual(provenance.library_versions("C#", self.root), {"Platform.Data.Doublets": "0.18.1"})

    def test_recording_preserves_measurements_and_captures_toolchain(self):
        path = self.root / "report.json"
        data = {"language": "Rust", "results": [{"median_ns": 1234}]}
        path.write_text(json.dumps(data))
        provenance.record(path, "rustc 1.98.1", self.root)
        recorded = json.loads(path.read_text())
        self.assertEqual(recorded["results"], data["results"])
        self.assertEqual(
            recorded["software"],
            {"libraries": {"doublets": "0.5.0"}, "toolchain": "rustc 1.98.1"},
        )

    def test_missing_manifests_cannot_silently_record_unknown_versions(self):
        (self.root / "rust/Cargo.lock").unlink()
        with self.assertRaises((ValueError, FileNotFoundError)):
            provenance.library_versions("Rust", self.root)


if __name__ == "__main__":
    unittest.main()
