using Microsoft.AspNetCore.Mvc;
using Stripe;
using TalenceInformatixs.Payments.Api.Models;
using TalenceInformatixs.Payments.Api.Services;

namespace TalenceInformatixs.Payments.Api.Controllers;

/// <summary>
/// Endpoints for processing real-time debit-card payments via Stripe.
/// </summary>
[ApiController]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IStripePaymentService _paymentService;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        IStripePaymentService paymentService,
        ILogger<PaymentsController> logger)
    {
        _paymentService = paymentService;
        _logger         = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // POST api/payments/debit
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Process a real-time debit-card payment.</summary>
    /// <remarks>
    /// Charges the customer's debit card immediately using Stripe's
    /// single-step confirm flow (PaymentIntent with confirm=true).
    ///
    /// **David Johnson → Talence Informatixs Inc, $100.00 USD**
    ///
    /// Sample request body:
    /// ```json
    /// {
    ///   "paymentMethodId": "pm_card_visa_debit",
    ///   "amount": 100.00,
    ///   "currency": "usd",
    ///   "customerName": "David Johnson",
    ///   "customerEmail": "david.johnson@example.com",
    ///   "merchantName": "Talence Informatixs Inc",
    ///   "description": "Service payment – David Johnson to Talence Informatixs Inc"
    /// }
    /// ```
    ///
    /// **Test cards (Stripe test mode):**
    /// | Token | Result |
    /// |---|---|
    /// | `pm_card_visa_debit` | ✅ Succeeds immediately |
    /// | `pm_card_visa_debitFundsWithdraw` | ✅ Succeeds (funds withdrawn) |
    /// | `pm_card_chargeDeclined` | ❌ Card declined |
    /// | `pm_card_chargeDeclinedInsufficientFunds` | ❌ Insufficient funds |
    /// | `pm_card_threeDSecure2Required` | 🔐 Requires 3-D Secure |
    /// </remarks>
    /// <param name="request">Debit payment details.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <response code="200">Payment authorised and captured successfully.</response>
    /// <response code="202">Payment initiated but requires 3-D Secure action.</response>
    /// <response code="400">Validation error in the request body.</response>
    /// <response code="402">Card declined or payment failed.</response>
    /// <response code="500">Unexpected server error.</response>
    [HttpPost("debit")]
    [ProducesResponseType(typeof(DebitPaymentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(DebitPaymentResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ProcessDebitPayment(
        [FromBody] DebitPaymentRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        _logger.LogInformation(
            "→ POST /api/payments/debit | Customer: {Customer} | Amount: {Amount} {Currency}",
            request.CustomerName, request.Amount, request.Currency.ToUpper());

        try
        {
            var result = await _paymentService.ProcessDebitPaymentAsync(request, ct);

            if (result.Success)
            {
                _logger.LogInformation("✅ Payment succeeded | TxId: {Id}", result.TransactionId);
                return Ok(result);
            }

            if (result.RequiresAction)
            {
                _logger.LogInformation("🔐 Payment requires 3DS action | TxId: {Id}", result.TransactionId);
                return Accepted(result);
            }

            _logger.LogWarning("⚠️ Payment not succeeded | Status: {Status}", result.Status);
            return StatusCode(StatusCodes.Status402PaymentRequired, new ApiErrorResponse
            {
                Error     = result.Message,
                Code      = result.Status,
                Status    = StatusCodes.Status402PaymentRequired
            });
        }
        catch (StripeException ex) when (ex.StripeError?.Type == "card_error")
        {
            _logger.LogWarning("❌ Card error: [{Code}] {Message}",
                ex.StripeError.Code, ex.StripeError.Message);

            return StatusCode(StatusCodes.Status402PaymentRequired, new ApiErrorResponse
            {
                Error  = ex.StripeError.Message ?? "Card was declined.",
                Code   = ex.StripeError.Code ?? "card_declined",
                Status = StatusCodes.Status402PaymentRequired
            });
        }
        catch (StripeException ex) when (ex.StripeError?.Type == "invalid_request_error")
        {
            _logger.LogWarning("❌ Stripe invalid request: {Message}", ex.Message);
            return BadRequest(new ApiErrorResponse
            {
                Error  = ex.StripeError?.Message ?? ex.Message,
                Code   = "invalid_request",
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/payments/debit/sample-request
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Returns a pre-filled sample request for the David Johnson → Talence Informatixs Inc scenario.</summary>
    /// <remarks>
    /// Handy for quickly testing the POST endpoint from Swagger UI.
    /// Copy this response body and paste it into the POST /api/payments/debit request body.
    /// </remarks>
    [HttpGet("debit/sample-request")]
    [ProducesResponseType(typeof(DebitPaymentRequest), StatusCodes.Status200OK)]
    public IActionResult GetSampleRequest() =>
        Ok(new DebitPaymentRequest
        {
            PaymentMethodId = "pm_card_visa_debit",
            Amount          = 100.00m,
            Currency        = "usd",
            CustomerName    = "David Johnson",
            CustomerEmail   = "david.johnson@example.com",
            MerchantName    = "Talence Informatixs Inc",
            Description     = "Service payment – David Johnson to Talence Informatixs Inc"
        });

    // ─────────────────────────────────────────────────────────────────────────
    // GET api/payments/debit/test-cards
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Lists Stripe test card tokens you can use in place of a real PaymentMethod ID.</summary>
    [HttpGet("debit/test-cards")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetTestCards() =>
        Ok(new
        {
            note = "Use these pm_* tokens as paymentMethodId when running in Stripe test mode.",
            cards = new[]
            {
                new { token = "pm_card_visa_debit",                        result = "Succeeds immediately" },
                new { token = "pm_card_visa_debitFundsWithdraw",           result = "Succeeds (funds withdrawn simulation)" },
                new { token = "pm_card_chargeDeclined",                    result = "Declined – generic" },
                new { token = "pm_card_chargeDeclinedInsufficientFunds",   result = "Declined – insufficient funds" },
                new { token = "pm_card_chargeDeclinedExpiredCard",         result = "Declined – expired card" },
                new { token = "pm_card_chargeDeclinedIncorrectCvc",        result = "Declined – incorrect CVC" },
                new { token = "pm_card_threeDSecure2Required",             result = "Requires 3-D Secure authentication" }
            }
        });
}
