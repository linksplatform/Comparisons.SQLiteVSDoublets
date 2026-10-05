"""NuGet's successful process exit is not proof of a clean dependency graph."""

import unittest

from audit_dotnet import vulnerabilities


class AuditTests(unittest.TestCase):
    def test_transitive_advisories_fail_the_audit(self):
        report = {
            "projects": [
                {
                    "path": "tests.csproj",
                    "frameworks": [
                        {
                            "transitivePackages": [
                                {
                                    "id": "Vulnerable.Package",
                                    "vulnerabilities": [
                                        {"severity": "High", "advisoryurl": "https://example.org/advisory"}
                                    ],
                                }
                            ]
                        }
                    ],
                }
            ]
        }
        self.assertEqual(vulnerabilities(report)[0][1], "Vulnerable.Package")

    def test_clean_graph_is_accepted(self):
        self.assertEqual(vulnerabilities({"projects": [{"path": "app.csproj", "frameworks": []}]}), [])

    def test_incomplete_audit_is_rejected(self):
        reports: list[dict] = [{}, {"projects": []}, {"errors": ["offline"], "projects": [{}]}]
        for report in reports:
            with self.subTest(report=report), self.assertRaises(ValueError):
                vulnerabilities(report)


if __name__ == "__main__":
    unittest.main()
