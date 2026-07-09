using System.ComponentModel.DataAnnotations;

namespace TalenceInformatixs.Payments.Api.Models;

// ─────────────────────────────────────────────────────────────────────────────
// REQUEST
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Payload for initiating a real-time debit-card payment.
/// The <c>paymentMethodId</c> (pm_xxxx) is created by Stripe.js in the browser
/// so that raw card numbers never reach this server.
/// </summary>
public sealed class DebitPaymentRequest
{
    /// <summary>
    /// Stripe PaymentMethod ID (pm_xxxx) tokenised by Stripe.js on the client.
    /// Use <c>pm_card_visa_debit</c> in Stripe test mode.
    /// </summary>
    /// <example>pm_card_visa_debit</example>
    [Required(ErrorMessage = "PaymentMethodId is required.")]
    public string PaymentMethodId { get; set; } = string.Empty;

    /// <summary>Payment amount in USD (dollars, not cents).</summary>
    /// <example>100.00</example>
    [Required]
    [Range(0.50, 999_999.99, ErrorMessage = "Amount must be between $0.50 and $999,999.99.")]
    public decimal Amount { get; set; }

    /// <summary>ISO 4217 currency code. Defaults to USD.</summary>
    /// <example>usd</example>
    [StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "usd";

    /// <summary>Optional human-readable description stored on the Stripe charge.</summary>
    /// <example>Service payment – David Johnson to Talence Informatixs Inc</example>
    [StringLength(500)]
    public string? Description { get; set; }

    // ── Customer ──────────────────────────────────────────────────────────────

    /// <summary>Full name of the paying customer.</summary>
    /// <example>David Johnson</example>
    [Required(ErrorMessage = "CustomerName is required.")]
    [StringLength(100)]
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Customer e-mail address (used to look up / create the Stripe Customer).</summary>
    /// <example>david.johnson@example.com</example>
    [EmailAddress]
    [StringLength(254)]
    public string? CustomerEmail { get; set; }

    // ── Merchant ──────────────────────────────────────────────────────────────

    /// <summary>Name of the receiving business / merchant.</summary>
    /// <example>Talence Informatixs Inc</example>
    [Required(ErrorMessage = "MerchantName is required.")]
    [StringLength(100)]
    public string MerchantName { get; set; } = string.Empty;
}

// ─────────────────────────────────────────────────────────────────────────────
// RESPONSE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Result of a debit-card payment attempt.</summary>
public sealed class DebitPaymentResponse
{
    /// <summary>True when the payment was captured successfully.</summary>
    public bool Success { get; set; }

    /// <summary>Stripe PaymentIntent ID (pi_xxxx).</summary>
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>Stripe Charge ID (ch_xxxx) – populated after capture.</summary>
    public string ChargeId { get; set; } = string.Empty;

    /// <summary>
    /// Stripe PaymentIntent status.
    /// Typical values: <c>succeeded</c> | <c>requires_action</c> | <c>requires_payment_method</c>
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Human-readable result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Amount charged in dollars.</summary>
    public decimal AmountCharged { get; set; }

    /// <summary>Currency (uppercase ISO 4217).</summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>Name of the paying customer.</summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>Name of the merchant / recipient.</summary>
    public string MerchantName { get; set; } = string.Empty;

    /// <summary>UTC timestamp of the authorization attempt.</summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Stripe client_secret returned when the card issuer requires 3-D Secure.
    /// Pass this to <c>stripe.handleNextAction(clientSecret)</c> on the client.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>True when the issuer requires additional authentication (3-D Secure).</summary>
    public bool RequiresAction { get; set; }

    /// <summary>Stripe receipt URL (populated on successful charge).</summary>
    public string? ReceiptUrl { get; set; }
}

// ─────────────────────────────────────────────────────────────────────────────
// ERROR
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>Standard error envelope returned on non-2xx responses.</summary>
public sealed class ApiErrorResponse
{
    public string Error   { get; set; } = string.Empty;
    public string Code    { get; set; } = string.Empty;
    public int    Status  { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
