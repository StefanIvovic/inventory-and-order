# Contract: `deploy/versions.env`

- Plain `KEY=VALUE` lines. No comments, no blank values, no quotes, no interpolation.
- Keys match `^[A-Z][A-Z0-9_]*_IMAGE$`.
- Each value is a full image reference: `[registry/]name:tag`.

Initial content:

```text
DOTNET_SDK_IMAGE=mcr.microsoft.com/dotnet/sdk:10.0.401-alpine3.24
DOTNET_ASPNET_IMAGE=mcr.microsoft.com/dotnet/aspnet:10.0.15-alpine3.24
POSTGRES_IMAGE=postgres:18.7-alpine3.24
RABBITMQ_IMAGE=rabbitmq:4.4.1-management-alpine
```

## Consumers (from later features)

- **Compose**: `image: ${POSTGRES_IMAGE}`. The root compose loads the file through `env_file`
  on `include:`, and a standalone service runs with `--env-file deploy/versions.env`.
- **Dockerfile**:
  ```text
  ARG DOTNET_SDK_IMAGE
  ARG DOTNET_ASPNET_IMAGE
  FROM ${DOTNET_SDK_IMAGE} AS build
  ```
  Compose passes the values in as `build.args`.
- **Testcontainers**: a BuildingBlocks testing helper reads `POSTGRES_IMAGE` and
  `RABBITMQ_IMAGE` from this file.
