# Inventory and Order

A multi-tenant inventory and order platform built as .NET 10 microservices. Every tenant has its own staff and customers, and every service owns its data, its roles and its permissions. Services talk to each other only through events on RabbitMQ, and the browser talks only to the gateway.

## Architecture

```mermaid
flowchart LR
    browser(["🌐 Browser"])
    gateway["<b>Gateway</b><br/>YARP + BFF<br/>the only public address"]

    backoffice["<b>Backoffice SPA</b><br/>/backoffice · staff"]
    storefront["<b>Storefront SPA</b><br/>/shop · customers"]

    subgraph identitybox ["Identity · /connect /account /api/identity"]
        direction LR
        identity["<b>Identity API</b><br/>sign-in, tokens,<br/>tenants, users"]
        identitydb[("Postgres")]
        identity --- identitydb
    end

    subgraph productbox ["Product · /api/product"]
        direction LR
        product["<b>Product API</b><br/>catalog"]
        productdb[("Postgres")]
        product --- productdb
    end

    subgraph inventorybox ["Inventory · /api/inventory"]
        direction LR
        inventory["<b>Inventory API</b><br/>stock"]
        inventorydb[("Postgres")]
        inventory --- inventorydb
    end

    subgraph orderbox ["Order · /api/order"]
        direction LR
        order["<b>Order API</b><br/>orders, saga"]
        orderdb[("Postgres")]
        order --- orderdb
    end

    subgraph paymentbox ["Payment · /api/payment"]
        direction LR
        payment["<b>Payment API</b><br/>payments"]
        paymentdb[("Postgres")]
        payment --- paymentdb
    end

    rabbit[["<b>RabbitMQ</b><br/>integration events<br/>sent after commit"]]

    browser ==>|"cookie only,<br/>no tokens"| gateway
    gateway --> backoffice
    gateway --> storefront
    gateway --> identitybox
    gateway --> productbox
    gateway --> inventorybox
    gateway --> orderbox
    gateway --> paymentbox

    identitybox -.-> rabbit
    productbox <-.-> rabbit
    inventorybox <-.-> rabbit
    orderbox <-.-> rabbit
    paymentbox <-.-> rabbit

    classDef client fill:#eef2f7,stroke:#64748b,color:#0f172a
    classDef edge fill:#dbeafe,stroke:#2563eb,color:#0f172a,stroke-width:2px
    classDef spa fill:#f1f5f9,stroke:#94a3b8,color:#0f172a
    classDef api fill:#dcfce7,stroke:#16a34a,color:#0f172a
    classDef db fill:#fef9c3,stroke:#ca8a04,color:#0f172a
    classDef bus fill:#ffedd5,stroke:#ea580c,color:#0f172a,stroke-width:2px

    class browser client
    class gateway edge
    class backoffice,storefront spa
    class identity,product,inventory,order,payment api
    class identitydb,productdb,inventorydb,orderdb,paymentdb db
    class rabbit bus

    style identitybox fill:#f8fafc,stroke:#94a3b8
    style productbox fill:#f8fafc,stroke:#94a3b8
    style inventorybox fill:#f8fafc,stroke:#94a3b8
    style orderbox fill:#f8fafc,stroke:#94a3b8
    style paymentbox fill:#f8fafc,stroke:#94a3b8
```

- **Gateway**: the only public address. It signs users in with Identity as a confidential OpenID Connect client and keeps tokens on the server, and forwards the access token to each API. The browser holds only an encrypted, HttpOnly cookie.
- **Identity**: issues short-lived access tokens that carry the user, tenant and user type only. It manages tenants, staff users and its own roles.
- **Each service**: validates the token itself, checks permissions from its own database, and keeps a local copy of the users it needs from Identity's events.
- **Messaging**: every change and its events are saved in one transaction, and events are sent only after commit (transactional outbox).

Built so far: Identity. The gateway, the two SPAs, and the Product, Inventory, Order and Payment services come next.

## Run it locally

### Prerequisites

- Docker Desktop (Compose 2.20 or later)
- .NET SDK 10.0.401 (pinned in `global.json`)
- PowerShell 7

### Start

```powershell
./scripts/restore-feed.ps1 -Service identity
./scripts/dev.ps1 up -Profile infra,apps
```

The first command builds the shared packages Identity depends on into the local feed (`artifacts/packages`). The second builds the images and starts RabbitMQ, the Identity database and Identity, then waits until they are healthy.

### Use

| What | Where |
|---|---|
| Identity sign-in | http://127.0.0.1:5101/account/login |
| Identity discovery document | http://127.0.0.1:5101/.well-known/openid-configuration |
| RabbitMQ management | http://127.0.0.1:15672 |
| Identity database | `127.0.0.1:5401` |

Accounts and passwords for development are in `deploy/dev.env`: the platform admin (`PLATFORM_ADMIN_*`) and the admin of the demo tenant (`DEMO_ADMIN_*`), both created on first start.

### Stop

```powershell
./scripts/dev.ps1 down    # stop, keep data
./scripts/dev.ps1 reset   # stop and delete data
```

### Tests

```powershell
dotnet test src/building-blocks/BuildingBlocks.slnx
dotnet test src/services/identity/Identity.slnx
```

Tests start their own throwaway containers and never touch the dev stack. Docker must be running.

More about the dev stack, ports and settings: [deploy/README.md](deploy/README.md).
