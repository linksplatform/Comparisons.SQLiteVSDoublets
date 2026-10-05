# Codacy Markdownlint

This is an integration created so that Codacy can run the [Markdownlint](https://github.com/DavidAnson/markdownlint)
tool in its infrastructure, or using its [CLI](https://github.com/codacy/codacy-analysis-cli).

## Generating documentation

```bash
npm install
npm run build:docs
```

## Build docker image

```bash
docker build -t codacy-markdownlint .
```

## Test changes to codacy-seed locally
You may need to test changes that comes from our [codacy-engine-typescript-seed](https://github.com/codacy/codacy-engine-typescript-seed).

1. Create a package with your changes on the seed:
    * Don't forget to update the dependencies: `npm install`
    * Compile the library: `npm run compile`
    * Package the library: `npm pack`
        > This should generate a codacy-seed-0.0.1.tgz on your codacy-seed repository

2.  Copy the `codacy-seed-0.0.1.tgz` into the root of this repository

3.  Install the package: `npm install codacy-seed-0.0.1.tgz`

4.  Update Dockerfile and `.dockerignore` so you copy the `codacy-seed-0.0.1.tgz` inside the docker you will be building
    *  Add `!codacy-seed-0.0.1.tgz` to your `.dockerignore`
    *  Add the package to the docker before `RUN npm install`: `COPY codacy-seed-0.0.1.tgz ./`
    *  Remove multi-stage docker steps
        *  Lines from `FROM node:$NODE_IMAGE_VERSION` to `RUN rm -rf /package.json /package-lock.json`
        > This way you skip copying the files to the other docker, and another `npm install`

5.  Publish your docker locally as normal: `docker build -t codacy-markdownlint .`

## Agent Playbook: Updating This Repository End-to-End

This section is written for an AI coding agent (or a human) tasked with updating this repo — most commonly bumping the wrapped [markdownlint](https://github.com/DavidAnson/markdownlint) version, but also Node base image or CircleCI orb bumps. Follow it top to bottom.

### 1. What this repository is

This is a **Codacy engine**: a Node.js/TypeScript wrapper (`src/index.ts`, `src/engineImpl.ts`, built on the `codacy-seed` npm package, which comes from [codacy-engine-typescript-seed](https://github.com/codacy/codacy-engine-typescript-seed)) that packages the [`markdownlint`](https://www.npmjs.com/package/markdownlint) npm library as a Docker image Codacy's platform can run against a customer's Markdown source. The `docs/` directory is machine-consumed configuration, not just documentation:

- `docs/patterns.json` — the full list of markdownlint rules ("patterns") Codacy knows about, their parameters/defaults, and which are enabled out of the box. Generated file, do not hand-edit.
- `docs/description/description.json` + `docs/description/MD*.md` — human-readable titles/descriptions per pattern (one `.md` per rule, e.g. `MD001.md`), used in the Codacy UI. Generated files, do not hand-edit.
- `docs/multiple-tests/{with-config-file,without-config-file,no-config}/*` — fixtures (`src/`, `patterns.xml`, `results.xml`) used by `codacy-plugins-test` to validate the engine's real output against sample Markdown files.
- `docs/tool-description.md` — short blurb about the tool, hand-maintained.

The two generated artifacts above come from **`DocGenerator`** (`src/docGenerator.ts`, run via `npm run build:docs` → `node dist/src/docGenerator.js`). It reads the rule list straight out of the installed `markdownlint` package (`node_modules/markdownlint/lib/rules.mjs` and `markdownlint`'s own `getVersion()`), then fetches each rule's Markdown doc page and the rule config JSON-schema from `https://raw.githubusercontent.com/DavidAnson/markdownlint/v<version>/...` on GitHub. This means the generator needs **network access** and a prior `npm install` (so the target markdownlint version is actually the one on disk) — it does NOT need git/pandoc/docker.

### 2. Files that encode versions — check all of these on every update

| File | What it controls | What to check |
|---|---|---|
| `package.json` → `dependencies.markdownlint` | Which markdownlint release is bundled and scraped by `DocGenerator` | Bump to the target version; run `npm install` so `package-lock.json` and `node_modules` match before regenerating docs. |
| `Dockerfile` → `FROM node:...-alpine...` (both stages) | Node.js runtime the build and the packaged app run on | Only bump if required; keep both `FROM` lines (builder and runtime stage) in sync. |
| `.circleci/config.yml` → `codacy/base`, `codacy/plugins-test`, `circleci/node` orbs | Shared CircleCI steps, plugin test runner, node-test job | Check the latest published orb versions if the task scopes an orb bump. |
| `tsconfig.json` | TypeScript target/lib, occasionally touched alongside a markdownlint bump (seen in a prior update) | Only touch if the new markdownlint version's types require it. |

### 3. Step-by-step update procedure

1. **Bump `markdownlint` in `package.json`** (`dependencies.markdownlint`), then `npm install` to refresh `package-lock.json` and pull the new version into `node_modules`.
2. **Regenerate the docs**: `npm run build:docs` (this runs `npm run build` first via the script, then `node dist/src/docGenerator.js`). Review the diff in `docs/patterns.json`, `docs/description/description.json`, and any new/removed/renamed `docs/description/MD*.md` files — new rules typically arrive disabled by default unless added to the enabled set; check `DocGenerator.isDefaultPattern`'s hard-coded `disabled` list in `src/docGenerator.ts` if a new rule should stay off by default.
3. **Compile**: `npm run build` (`tsc --showConfig && tsc`) must succeed with no type errors — a version bump can change `markdownlint`'s exported types.
4. **Lint**: `npm run lint` (`eslint --fix --ext .ts ./src`).
5. **Run the native test suite**: `npm test` (mocha over `src/test/**/*.spec.ts`, covers `computeSuggestion`, `convertResults`, and an `integration` spec). Update any test fixtures/expectations that reference specific rule behavior if the new markdownlint version changed messages or defaults.
6. **Build the Docker image**: `docker build -t codacy-markdownlint .`.
7. **Run `codacy-plugins-test` locally** before pushing — clone https://github.com/codacy/codacy-plugins-test and run its DockerTest commands (matching the CI's `run_multiple_tests: true` mode, i.e. exercising all three fixtures under `docs/multiple-tests/`) against your local image tag. Update `docs/multiple-tests/*/results.xml` if rule output legitimately changed.
8. **Iterate on failures**, re-running only the relevant command after each fix.
9. **Commit** the version bump together with the regenerated `docs/` files and any test fixture updates in one change.
10. **Push and open a PR.**
11. **Poll the PR's real CI checks until they all pass — local validation is NOT the finish line.** CircleCI runs `codacy/checkout_and_version` → `node/test` (`npm test`) and `codacy/shell` (docker build) in parallel → `codacy_plugins_test/run` (with `run_multiple_tests: true`) → (on `master` only) `codacy/publish_docker` → `codacy/tag_version`. After every push, check the PR's CI status (e.g. `gh pr checks <pr-url>`) and keep re-polling while any check is pending. If a check fails, fetch its actual log, find the true root cause, fix it, push again (never `--no-verify`, never force-push), and re-poll. Repeat until every check is green. The CI environment's toolchain can differ from your local one, so a clean local run does not guarantee CI passes. Only stop iterating when every check passes, or you hit a genuine product/infra decision that needs a human.

### 4. Common failure modes and fixes

| Symptom | Likely cause | Fix |
|---|---|---|
| `DocGenerator` logs "Failed to retrieve docs for `MD0xx`" and/or a description file is empty | The markdownlint GitHub tag `v<version>` doesn't have a `doc/<ruleid>.md` at that path (renamed/moved doc, or version bump ran before `npm install` picked up the new version) | Confirm `npm install` actually installed the target version (`node_modules/markdownlint/package.json`) before running `build:docs`; check the actual doc path in the `DavidAnson/markdownlint` repo at that tag. |
| New pattern IDs appear in `docs/patterns.json` unexpectedly enabled/disabled | `DocGenerator.isDefaultPattern`'s hard-coded `disabled` array in `src/docGenerator.ts` wasn't updated for a newly introduced rule | Decide the intended default for the new rule and add/remove it from that array, then regenerate. |
| `docker build` fails on the builder stage during `npm run build:docs` | No network access in the build environment (the doc generator needs to reach `raw.githubusercontent.com`) | Regenerate docs locally (outside Docker) with `npm run build:docs` and commit the resulting `docs/` changes; the Docker build only re-runs it as a side effect of `build:docs` being part of the builder stage. |

### 5. Definition of done

- `markdownlint` version bumped in `package.json` (and `package-lock.json` regenerated via `npm install`).
- Docker base image version, if bumped, updated consistently in both `FROM` lines in `Dockerfile`.
- `docs/patterns.json`, `docs/description/description.json`, and `docs/description/MD*.md` regenerated via `npm run build:docs`, with new/removed rules reviewed and default-enabled state confirmed.
- `npm run build`, `npm run lint`, and `npm test` all pass locally.
- Docker image builds successfully.
- `codacy-plugins-test` commands (including the multi-fixture run) pass locally against the freshly built image; `docs/multiple-tests/*/results.xml` updated if output legitimately changed.
- **After pushing and opening/updating the PR, every CI check on it is green.** Poll `gh pr checks <pr-url>` and iterate on any failure until all pass.

## What is Codacy

[Codacy](https://www.codacy.com/) is an Automated Code Review Tool that monitors your technical debt, helps you improve your code quality, teaches best practices to your developers, and helps you save time in Code Reviews.

### Among Codacy’s features

- Identify new Static Analysis issues
- Commit and Pull Request Analysis with GitHub, BitBucket/Stash, GitLab (and also direct git repositories)
- Auto-comments on Commits and Pull Requests
- Integrations with Slack, HipChat, Jira, YouTrack
- Track issues in Code Style, Security, Error Proneness, Performance, Unused Code and other categories

Codacy also helps keep track of Code Coverage, Code Duplication, and Code Complexity.

Codacy supports PHP, Python, Ruby, Java, JavaScript, and Scala, among others.

### Free for Open Source

Codacy is free for Open Source projects.
