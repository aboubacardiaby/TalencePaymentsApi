# Talence Informatixs – Debit Payment API

A production-ready ASP.NET Core 8 Web API for processing **real-time debit-card payments** via Stripe.

Scenario: **David Johnson** pays **$100.00 USD** to **Talence Informatixs Inc**.

---

## Features

- ✅ Real-time debit-card authorization (single round-trip via `PaymentIntent confirm=true`)
- ✅ Stripe Customer auto-resolve (reuse existing customer by email)
- ✅ 3-D Secure (3DS2) support with `clientSecret` passback
- ✅ Stripe webhook handler with HMAC signature verification
- ✅ Swagger / OpenAPI UI at application root (`/`)
- ✅ Structured logging via Serilog (console + rolling file)
- ✅ Global exception middleware with clean JSON error responses
- ✅ Health / readiness probes (`/api/health/live`, `/api/health/ready`)
- ✅ Pre-filled sample request endpoint and test-card reference

---

## Prerequisites

| Tool | Version |
|---|---|
| .NET SDK | 8.0 or later |
| Stripe account | Free test account |

---

## Quick Start

### 1. Clone / unzip the project

```bash
cd TalencePaymentsApi
```

### 2. Add your Stripe keys

Edit `appsettings.json` (or use user-secrets / environment variables):

```json
{
  "Stripe": {
    "SecretKey":     "sk_test_YOUR_KEY_HERE",
    "PublishableKey":"pk_test_YOUR_KEY_HERE",
    "WebhookSecret": "whsec_YOUR_WEBHOOK_SECRET_HERE"
  }
}
```

> Get your keys at [dashboard.stripe.com/apikeys](https://dashboard.stripe.com/apikeys)

### 3. Run

```bash
dotnet run
```

The API starts on `http://localhost:5000` (or `https://localhost:5001`).  
Swagger UI opens automatically at the root URL.

---

## API Endpoints

### `POST /api/payments/debit`

Processes a real-time debit-card payment.

**Request body:**
```json
{
  "paymentMethodId": "pm_card_visa_debit",
  "amount": 100.00,
  "currency": "usd",
  "customerName": "David Johnson",
  "customerEmail": "david.johnson@example.com",
  "merchantName": "Talence Informatixs Inc",
  "description": "Service payment – David Johnson to Talence Informatixs Inc"
}
```

**Success response (200):**
```json
{
  "success": true,
  "transactionId": "pi_3PxABC...",
  "chargeId": "ch_3PxABC...",
  "status": "succeeded",
  "message": "Payment of $100.00 successfully authorised and captured.",
  "amountCharged": 100.00,
  "currency": "USD",
  "customerName": "David Johnson",
  "merchantName": "Talence Informatixs Inc",
  "timestamp": "2026-06-13T14:22:01Z",
  "receiptUrl": "https://pay.stripe.com/receipts/...",
  "requiresAction": false,
  "clientSecret": null
}
```

**3-D Secure required (202):**
```json
{
  "requiresAction": true,
  "clientSecret": "pi_xxx_secret_yyy",
  "status": "requires_action"
}
```
→ Pass `clientSecret` to `stripe.handleNextAction(clientSecret)` on the client.

**Card declined (402):**
```json
{
  "error": "Your card has insufficient funds.",
  "code": "insufficient_funds",
  "status": 402
}
```

---

### `GET /api/payments/debit/sample-request`

Returns the pre-filled David Johnson request body ready to copy into the POST endpoint.

### `GET /api/payments/debit/test-cards`

Lists all Stripe test `pm_*` tokens and their expected behaviour.

### `POST /api/webhooks/stripe`

Receives Stripe webhook events. Register this URL in the Stripe Dashboard.

### `GET /api/health/live` / `GET /api/health/ready`

Liveness and readiness probes (Stripe connectivity check).

---

## Test Cards

Use these `pm_*` tokens as `paymentMethodId` in test mode:

| Token | Result |
|---|---|
| `pm_card_visa_debit` | ✅ Succeeds immediately |
| `pm_card_visa_debitFundsWithdraw` | ✅ Succeeds (funds withdrawn) |
| `pm_card_chargeDeclined` | ❌ Declined – generic |
| `pm_card_chargeDeclinedInsufficientFunds` | ❌ Declined – insufficient funds |
| `pm_card_chargeDeclinedExpiredCard` | ❌ Declined – expired card |
| `pm_card_chargeDeclinedIncorrectCvc` | ❌ Declined – incorrect CVC |
| `pm_card_threeDSecure2Required` | 🔐 Requires 3-D Secure |

---

## Testing Webhooks Locally

Install the Stripe CLI and forward events to your local server:

```bash
stripe listen --forward-to http://localhost:5000/api/webhooks/stripe
```

The CLI prints a `whsec_...` signing secret — paste it into `appsettings.json`.

Then trigger a test event:

```bash
stripe trigger payment_intent.succeeded
```

---

## Project Structure

```
TalencePaymentsApi/
├── Controllers/
│   ├── PaymentsController.cs      # POST /api/payments/debit
│   ├── WebhooksController.cs      # POST /api/webhooks/stripe
│   └── HealthController.cs        # GET  /api/health/*
├── Models/
│   └── PaymentModels.cs           # Request, Response, Error models
├── Services/
│   ├── IStripePaymentService.cs   # Service interface
│   └── StripePaymentService.cs    # Stripe integration logic
├── Middleware/
│   └── GlobalExceptionMiddleware.cs
├── Configuration/
│   └── StripeSettings.cs          # Strongly-typed config
├── Program.cs                     # App bootstrap + DI
├── appsettings.json
├── appsettings.Development.json
└── TalencePaymentsApi.csproj
```

---

## Environment Variables (alternative to appsettings)

```bash
export Stripe__SecretKey="sk_test_..."
export Stripe__WebhookSecret="whsec_..."
export Stripe__PublishableKey="pk_test_..."
dotnet run
```

---

## Logs

Rolling log files are written to `logs/talence-payments-YYYYMMDD.log`.

---

## License

MIT – Talence Informatixs Inc © 2026
