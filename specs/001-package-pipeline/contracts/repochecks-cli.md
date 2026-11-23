# Contract: `RepoChecks` tool

```text
dotnet run --project tools/RepoChecks -- images [--root .]
```

## Output format

One line per finding:

```text
<relative-path>:<line>: <rule-id> <detail>
```

Exit code `0` means no findings, `1` means findings, and `2` means a usage or configuration error.

Scanned files: `deploy/versions.env`, `global.json`, every `compose*.yaml`/`compose*.yml` and
`Dockerfile*`, and every `*.cs` file under a `*.Tests` project.

Excluded folders: `bin`, `obj`, `node_modules`, `.git`, `.specify`, `.claude`, `artifacts`,
`specs`.

## `images` rules

| Rule id | Detects |
|---|---|
| `IP001` | A `versions.env` line that is not `KEY=VALUE`, or a key that doesn't end in `_IMAGE` |
| `IP002` | A `versions.env` value with tag `latest`, no tag, or a floating tag (major-only, or major.minor for components that publish patches) |
| `IP003` | A compose `image:` that isn't exactly `${KEY}` |
| `IP004` | A compose or Dockerfile variable that `versions.env` doesn't define |
| `IP005` | A Dockerfile `FROM` not using an ARG, or the ARG has a default value |
| `IP006` | A test source string literal containing `<known-repo>:` |
| `IP007` | The `DOTNET_SDK_IMAGE` tag doesn't match the `global.json` SDK version |
| `IP008` | The same image repository defined under more than one key in `versions.env` |
