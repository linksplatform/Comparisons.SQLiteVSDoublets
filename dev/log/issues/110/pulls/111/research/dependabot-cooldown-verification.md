# Dependabot configuration verification

Verified on 2026-10-05 against
[GitHub's options reference](https://docs.github.com/en/code-security/reference/supply-chain-security/dependabot-options-reference).

The `cooldown` support table includes Cargo, NuGet, Pip and GitHub Actions.
`default-days` applies to these ecosystems; only the SemVer-specific day options
have additional ecosystem restrictions. The repository uses `default-days: 7`.
Cooldown does not delay security updates. `directories` supports a list of
manifest locations; the GitHub Actions location remains `/`.
