using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using TalenceInformatixs.Payments.Api.Configuration;

namespace TalenceInformatixs.Payments.Api.Controllers;

/// <summary>
/// Receives and verifies asynchronous Stripe webhook events.
///
/// Register this URL in your Stripe Dashboard → Developers → Webhooks:
///   https://&lt;your-domain&gt;/api/webhooks/stripe
///
/// Events handled:
///   • payment_intent.succeeded
///   • payment_intent.payment_failed
///   • charge.succeeded
///   • charge.refunded
/// </summary>
[ApiController]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly ILogger<WebhooksController> _logger;
    private readonly string _webhookSecret;

    public WebhooksController(
        ILogger<WebhooksController> logger,
        IOptions<StripeSettings> settings)
    {
        _logger        = logger;
        _webhookSecret = settings.Value.WebhookSecret;
    }

    /// <summary>
    /// Stripe webhook endpoint. Verifies the <c>Stripe-Signature</c> header
    /// and dispatches to the appropriate handler.
    /// </summary>
    /// <remarks>
    /// ⚠️ This endpoint must be excluded from any CSRF / anti-forgery middleware
    /// and must read the raw request body to validate the HMAC signature.
    /// </remarks>
    [HttpPost("stripe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> HandleStripeWebhook()
    {
        string json;
        using (var reader = new StreamReader(HttpContext.Request.Body))
            json = await reader.ReadToEndAsync();

        // ── Verify the Stripe-Signature header ────────────────────────────────
        if (!Request.Headers.TryGetValue("Stripe-Signature", out var signature))
        {
            _logger.LogWarning("Stripe webhook received without Stripe-Signature header.");
            return BadRequest("Missing Stripe-Signature header.");
        }

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(json, signature, _webhookSecret);
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe webhook signature verification failed.");
            return BadRequest("Webhook signature verification failed.");
        }

        _logger.LogInformation("Received Stripe event: {Type} | Id: {Id}",
            stripeEvent.Type, stripeEvent.Id);

        // ── Dispatch ──────────────────────────────────────────────────────────
        switch (stripeEvent.Type)
        {
            case "payment_intent.succeeded":
                await HandlePaymentSucceededAsync(stripeEvent);
                break;

            case "payment_intent.payment_failed":
                await HandlePaymentFailedAsync(stripeEvent);
                break;

            case "charge.succeeded":
                await HandleChargeSucceededAsync(stripeEvent);
                break;

            case "charge.refunded":
                await HandleChargeRefundedAsync(stripeEvent);
                break;

            default:
                _logger.LogDebug("Unhandled Stripe event type: {Type}", stripeEvent.Type);
                break;
        }

        return Ok(new { received = true });
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private Task HandlePaymentSucceededAsync(Event e)
    {
        if (e.Data.Object is not PaymentIntent intent) return Task.CompletedTask;

        var customer = intent.Metadata.TryGetValue("customer_name", out var name) ? name : "unknown";
        var merchant = intent.Metadata.TryGetValue("merchant_name", out var m)    ? m    : "unknown";

        _logger.LogInformation(
            "✅ PaymentIntent SUCCEEDED | Id: {Id} | Customer: {Customer} | " +
            "Merchant: {Merchant} | Amount: {Amount} {Currency}",
            intent.Id, customer, merchant,
            intent.Amount / 100m, intent.Currency.ToUpper());

        // TODO: update order status in your database
        // TODO: send payment confirmation email to customer
        // TODO: notify merchant / accounting system

        return Task.CompletedTask;
    }

    private Task HandlePaymentFailedAsync(Event e)
    {
        if (e.Data.Object is not PaymentIntent intent) return Task.CompletedTask;

        var reason = intent.LastPaymentError?.Message ?? "Unknown reason";
        var code   = intent.LastPaymentError?.Code   ?? "unknown_error";

        _logger.LogWarning(
            "❌ PaymentIntent FAILED | Id: {Id} | Code: {Code} | Reason: {Reason}",
            intent.Id, code, reason);

        // TODO: notify customer of failure
        // TODO: trigger retry logic if applicable

        return Task.CompletedTask;
    }

    private Task HandleChargeSucceededAsync(Event e)
    {
        if (e.Data.Object is not Charge charge) return Task.CompletedTask;

        _logger.LogInformation(
            "💳 Charge SUCCEEDED | Id: {Id} | Customer: {Customer} | " +
            "Amount: {Amount} {Currency} | Receipt: {Receipt}",
            charge.Id,
            charge.BillingDetails?.Name ?? "N/A",
            charge.Amount / 100m,
            charge.Currency.ToUpper(),
            charge.ReceiptUrl ?? "N/A");

        return Task.CompletedTask;
    }

    private Task HandleChargeRefundedAsync(Event e)
    {
        if (e.Data.Object is not Charge charge) return Task.CompletedTask;

        _logger.LogInformation(
            "↩️  Charge REFUNDED | Id: {Id} | AmountRefunded: {Refunded} {Currency}",
            charge.Id,
            charge.AmountRefunded / 100m,
            charge.Currency.ToUpper());

        // TODO: update your records and notify customer

        return Task.CompletedTask;
    }
}
