# B506 falsely reports arbitrary-object construction with PyYAML BaseLoader

Environment: Bandit 1.9.4, PyYAML 6.0.3, Python 3.14.7. Also observed in the
Codacy and CodeFactor checks on Comparisons.SQLiteVSDoublets PR 111.

## Minimal reproduction

Save as `fixture.py`:

```python
import yaml
print(yaml.load("!!python/object:collections.Counter {}", Loader=yaml.BaseLoader))
```

Run `python fixture.py`: it prints `{}`, a plain dictionary, without constructing
a Counter. Run `bandit -t B506 -f json fixture.py`: exit 1, B506, medium severity,
high confidence, stating that the loader allows instantiation of arbitrary objects.

PyYAML documents BaseLoader as supporting no tags and constructing only strings,
lists and dictionaries:
https://pyyaml.org/wiki/PyYAMLDocumentation#LoadingYAML

The current `bandit/plugins/yaml_load.py` only recognizes SafeLoader/CSafeLoader,
so BaseLoader/CBaseLoader are classified as unsafe despite their smaller set of
constructible types. This affects applications intentionally preserving all YAML
scalars as strings, including GitHub workflow `on` keys.

## Workarounds

Use `yaml.safe_load` and normalize YAML 1.1 boolean interpretation of an unquoted
`on` key and boolean policies, or use a narrowly justified `# nosec B506` for a
reviewed BaseLoader call. Our PR takes the former approach and adds tests for
event preservation, boolean cancellation, invalid mappings and object-tag rejection.

## Suggested fix

Recognize `BaseLoader` and `CBaseLoader` alongside the two SafeLoader classes for
both keyword and positional Loader arguments. Add regression cases for these
four safe classes; retain failures for Loader, CLoader and UnsafeLoader. Alias
resolution can be considered separately rather than weakening checks globally.

Related investigation and reusable probe:
https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/pull/111

The local JSON result and plugin source are preserved in that PR's
`dev/log/issues/110/pulls/111/` evidence archive.
