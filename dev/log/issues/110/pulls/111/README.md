# Issue 110 / PR 111 evidence

[Analysis](ANALYSIS.md) lists every requirement, reconstructs the timeline,
identifies root causes and compares all three templates and all 16 principles.
[Solution plans](SOLUTION_PLANS.md) describe alternatives and the implemented
choices. [Execution plan](PLAN.md) records the completed work.

The complete raw collection is in [raw-evidence.tar.gz](raw-evidence.tar.gz).
It contains the original `ci-logs`, `github`, `research`, `templates` and
`validation` directories. Every original path cited in the analysis is retained
inside the archive. [manifest.json](manifest.json) records file sizes and SHA-256
hashes; [diagnostic-index.json](diagnostic-index.json) records original log paths
and line numbers with excerpts. Complete lines remain in the archived logs.

From the repository root, extract and verify the evidence:

```sh
tar -xzf dev/log/issues/110/pulls/111/raw-evidence.tar.gz \
  -C dev/log/issues/110/pulls/111
python experiments/ci/package_evidence.py dev/log/issues/110/pulls/111 --verify
```

Extracted directories are ignored by Git. Keeping their original bytes in a
compressed archive avoids GitHub's diff limits and the observed Codacy PR-diff
timeout while preserving a readable source diff and all investigation data.

To refresh the collection, run these commands in order:

```sh
python experiments/ci/collect_evidence.py
python experiments/ci/package_evidence.py dev/log/issues/110/pulls/111
python experiments/ci/index_evidence.py dev/log/issues/110/pulls/111
python experiments/ci/package_evidence.py dev/log/issues/110/pulls/111 --verify
```
