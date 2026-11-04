# Consumer Hand-off - Defra People API

For teams that call the People API (confirmed consumer today: `baqu`). **Existing `X-API-Key` authentication and endpoint contracts are unchanged.**

## Contract (unchanged)

| Request | Result |
|---|---|
| `GET /getusers/by-sid/{id}` | User details looked up by Microsoft Graph SID |
| `GET /getusers/by-mailnickname/{id}` | User details looked up by mail nickname |
| `GET /getusers/by-samaccount/{id}` | User details looked up by SAM account name |

- All three require the `X-API-Key` header (unchanged) and the `X-State` header (any non-empty value; echoed back on the response for correlation).
- Optional `X-GetManager: true` header also returns the user's manager.
- Responses are `200 OK` with the user JSON, or `400 Bad Request` if `X-State` is missing.
- API documentation: `/swagger` (enabled by default only in Development/non-production - see [Operations-Notes.md](./Operations-Notes.md)).

## What's new (opt-in, nothing required from consumers)

- **Entra ID bearer tokens are now also accepted** as an alternative to `X-API-Key`, if the service operator configures `AzureAd:TenantId`/`Audience`. Existing API-key based integrations (including `baqu`) do **not** need to change.
- **CORS** is now explicit and deny-by-default; browser-based consumers need their origin added to `Cors:AllowedOrigins` by the service operator. Server-to-server consumers (current pattern) are unaffected.
- The service can now run in a container (`people-api/Dockerfile`); the base address/port may change depending on the hosting platform chosen - confirm the new URL with the platform team before cutover.

## Health and availability

`GET {base}/health/live` (process) and `GET {base}/health/ready` (Microsoft Graph + log database reachable) are available for monitoring. Both are anonymous.

## Admin endpoints (unchanged, API key with Admin policy only)

`GET /admin/list-api-keys`, `POST /admin/generate-api-key`, `POST /admin/refresh-api-keys` remain restricted to callers presenting the admin API key. These are not intended for general consumers.
