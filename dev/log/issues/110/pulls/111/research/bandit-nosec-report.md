Confirmed with Bandit **1.9.4** and Python **3.13.15** while validating [Comparisons.SQLiteVSDoublets PR 111](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/pull/111). A nested postprocessing call also reproduces this:

```python
import subprocess
value = subprocess.run(["/usr/bin/true"]).stdout.strip()  # nosec B603
```

Run `bandit -q -t B603 -f json fixture.py`. Without the annotation it returns one B603 finding and exit 1. With the correct annotation it returns no findings and exit 0, but emits `nosec encountered (B603), but no failed test` for the same line.

The workaround avoids nested calls on the annotation's line:

```python
import subprocess
result = subprocess.run(["/usr/bin/true"])  # nosec B603
value = result.stdout.strip()
```

This returns no findings and no warning. The fixture is only scanned; its subprocess is never executed. A [three-case automated probe](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/blob/issue-110-e1f96d946132/experiments/ci/bandit_nosec_probe.py) asserts the findings, process exits and warning presence; [output](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/blob/issue-110-e1f96d946132/dev/log/issues/110/pulls/111/validation/bandit-nosec-reproduction.log) is preserved.

In `bandit/core/tester.py`, the `result is None` branch emits a warning for the outer call even though another AST call on that line consumes the annotation correctly. A possible fix is to record consumed `(file, line, test ID)` annotations and defer unused-annotation warnings until all nodes in the file are visited. Emit each warning once and retain warnings for truly unused test IDs. Regression cases should include both this chained call and the nested-argument example above.
