## Reproduction

At commit `22e53c87d87274415eb11c291702597c4021c50d`, `.github/workflows/release.yml:242` still uses
`os: [ubuntu-24.04, macos-latest, windows-latest]`.

```sh
rg -n 'os:.*latest' .github/workflows/release.yml
```

No check rejects these floating labels. Hosted runner migrations can change
the environment independently of a repository commit. This leaves the same
runner-drift class of failure we investigated in
[linksplatform/Comparisons.SQLiteVSDoublets#110](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/issues/110):
[run 37294124568](https://github.com/linksplatform/Comparisons.SQLiteVSDoublets/actions/runs/37294124568)
requests LLVM 13 on the current Noble `ubuntu-latest` image and fails because
the apt repository does not exist. We have not observed a macOS or Windows
SDK failure in this template; the reproduced defect is the unpinned matrix.

## Workaround and suggested fix

Pin all matrix entries, for example `ubuntu-24.04`, `macos-15`, and
`windows-2025`, after testing the supported SDK on those images. Add a test
that parses every workflow's `runs-on` and `strategy.matrix.os` and rejects
labels ending in `-latest`. Schedule intentional image migrations. This follows
principle 1 of
[CI-CD-BEST-PRACTICES.md](https://github.com/link-assistant/hive-mind/blob/main/docs/CI-CD-BEST-PRACTICES.md).

The Rust template already has the corresponding report #180; this report
covers the C# template's remaining floating matrix entries.
