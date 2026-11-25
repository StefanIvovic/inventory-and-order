# Contract: Routes and forwarding

| Path | Upstream | Path sent | Session | Cookies forwarded | Authorization forwarded |
|---|---|---|---|---|---|
| `/connect/**`, `/account/**`, `/.well-known/**` | Identity | unchanged | no | all except the gateway's own | browser's |
| `/backoffice/**` | backoffice SPA | unchanged | no | all except the gateway's own | browser's |
| `/shop/**` | storefront SPA | unchanged | no | all except the gateway's own | browser's |
| `/api/identity/**` | Identity | prefix removed | yes | none | gateway's bearer only |
| `/bff/**`, `/health/live`, `/health/ready` | the gateway itself | | see bff.md | | |
| anything else | | | | | `404` |

- **Every forwarded request**: `X-Forwarded-For`, `X-Forwarded-Proto` and `X-Forwarded-Host` are set by the gateway, replacing anything the client sent. W3C `traceparent` is propagated. `X-Bff-App` is not forwarded.
- **Adding a service**: one route `api-<service>` (`/api/<service>/{**rest}`, `PathRemovePrefix`, metadata `Session: required`) and one cluster, in configuration only.

## Identity behind the gateway

- **Forwarded headers**: honored only from `Identity:ForwardedHeaders:KnownNetworks` (dev: `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`), `ForwardLimit` 1. `X-Forwarded-Host` is accepted only for `Identity:ForwardedHeaders:AllowedHosts` (dev: `localhost:8080`).
- **Stylesheet**: `/account/css/identity.css`.
- **Dev stack**: `identity-api` publishes no port.
