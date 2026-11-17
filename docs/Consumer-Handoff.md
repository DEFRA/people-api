# Consumer Hand-off - Defra People API

For teams that call the People API (confirmed consumer today: `baqu`). **`X-API-Key` authentication has been removed.** Every request must now present a Microsoft Entra ID bearer token carrying the `People.Read` or `People.Admin` app role. The endpoint contracts and response shapes below are otherwise unchanged.

## Contract

| Request | Result |
|---|---|
| `GET /getusers/by-sid/{id}` | User details looked up by Microsoft Graph SID |
| `GET /getusers/by-mailnickname/{id}` | User details looked up by mail nickname |
| `GET /getusers/by-samaccount/{id}` | User details looked up by SAM account name |

- All three require `Authorization: Bearer <token>` where the token's `roles` claim contains `People.Read` or `People.Admin` (role token values are configurable per environment - confirm the live values with the service operator), and the `X-State` header (any non-empty value; echoed back on the response for correlation).
- Optional `X-GetManager: true` header also returns the user's manager.
- Responses are `200 OK` with the user JSON, `400 Bad Request` if `X-State` is missing, `401 Unauthorized` if the token is missing/invalid, or `403 Forbidden` if the token is valid but lacks the required role.
- API documentation: `/swagger` (enabled by default only in Development/non-production - see [Operations-Notes.md](./Operations-Notes.md)).

## Migrating from `X-API-Key`

1. Register (or ask the identity team to register) your own Entra ID identity - a **managed identity** if you're Azure-hosted, workload identity federation where supported, or a certificate otherwise. Do not share an identity across consumers.
2. Ask the People API's operator to run `infra/entra/Grant-PeopleApiRole.ps1` to assign your identity to `People.Read` (or `People.Admin` if you also need the maintenance endpoints below).
3. Acquire and cache an access token for the People API's Application ID URI (`api://<people-api-client-id>`) using your identity, and send it as `Authorization: Bearer <token>` on every call. Token lifetime and your own caching strategy determine how quickly a role change or revocation takes effect - a token already issued remains usable until it expires.
4. Remove the `X-API-Key` header from your requests - it is no longer read.

## Other changes

- **CORS** is explicit and deny-by-default; browser-based consumers need their origin added to `Cors:AllowedOrigins` by the service operator. Server-to-server consumers (the expected pattern for this API) are unaffected.
- The service runs in a container (`people-api/Dockerfile`); confirm the current base URL with the platform team.

## Health and availability

`GET {base}/health/live` (process) and `GET {base}/health/ready` (Microsoft Graph reachable) are available for monitoring. Both are anonymous.

## Admin endpoints (`People.Admin` role only)

`GET /admin/dump-cache` and `POST /admin/purge-cache` require a bearer token carrying the `People.Admin` app role. These are operational endpoints, not intended for general consumers.
