# Quickstart: Product Service Skeleton

## 1. BuildingBlocks

```powershell
dotnet test src/building-blocks/BuildingBlocks.slnx
./scripts/pack.ps1
```

The authentication tests cover the internal-address backchannel ([research R1](research.md#r1-validating-identitys-tokens-from-inside-docker)). The 0.5.0 tag is pushed only with the owner's approval.

## 2. Product service

```powershell
dotnet test --solution src/services/product/Product.slnx
./scripts/codegen.ps1 -Service product -Check
```

This covers health, authentication (401 cases), `GET /roles` (403 without the permission, 200 with a granted role in a test), the no-roles route test, the role table and the contracts package.

## 3. Gateway

```powershell
dotnet test --solution src/gateway/Gateway.slnx
```

The route-table test includes `/api/product`.

## 4. Dev stack (never reset)

```powershell
./scripts/dev.ps1 up -Profile infra,apps
docker exec inventory-and-order-product-api-1 wget -q -O /dev/null http://localhost:8080/health/ready
```

`product-api` and `product-db` are healthy, and `docker port inventory-and-order-product-api-1` prints nothing. Signed in to the backoffice as the demo admin, `GET /api/product/roles` with the `X-Bff-App: backoffice` header answers 403 listing `product.roles.view`.

## 5. End-to-end

```powershell
./scripts/e2e.ps1
```

All specs pass, including the Product route check, and nothing named `e2e-test` remains.
