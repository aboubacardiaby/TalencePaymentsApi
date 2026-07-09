using Microsoft.Extensions.Options;
using Stripe;
using TalenceInformatixs.Payments.Api.Configuration;
using TalenceInformatixs.Payments.Api.Models;

namespace TalenceInformatixs.Payments.Api.Services;

/// <summary>
/// Implements real-time debit-card payment processing via the Stripe API.
///
/// Flow:
///   1. Resolve or create a Stripe Customer for the payer.
///   2. Create a PaymentIntent with confirm=true (single round-trip auth + capture).
///   3. Map the Stripe response to <see cref="DebitPaymentResponse"/>.
/// </summary>
public sealed class StripePaymentService : IStripePaymentService
{
    private readonly CustomerService      _customers;
    private readonly PaymentIntentService _paymentIntents;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly StripeSettings       _settings;

    public StripePaymentService(
        CustomerService customers,
        PaymentIntentService paymentIntents,
        ILogger<StripePaymentService> logger,
        IOptions<StripeSettings> settings)
    {
        _customers      = customers;
        _paymentIntents = paymentIntents;
        _logger         = logger;
        _settings       = settings.Value;
    }

    /// <inheritdoc/>
    public async Task<DebitPaymentResponse> ProcessDebitPaymentAsync(
        DebitPaymentRequest request,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "Starting debit payment | Customer: {Customer} | Merchant: {Merchant} | Amount: {Amount} {Currency}",
            request.CustomerName, request.MerchantName, request.Amount, request.Currency.ToUpper());

        // ── Step 1: Resolve / create the Stripe Customer ──────────────────────
        var customerId = await EnsureCustomerAsync(request, ct);
        _logger.LogDebug("Resolved Stripe Customer: {CustomerId}", customerId);

        // ── Step 2: Convert dollars → cents (Stripe smallest-currency-unit) ───
        var amountCents = (long)Math.Round(request.Amount * 100, MidpointRounding.AwayFromZero);

        // ── Step 3: Build the PaymentIntent ───────────────────────────────────
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? $"Debit payment from {request.CustomerName} to {request.MerchantName}"
            : request.Description;

        // Statement descriptor max 22 chars, letters/digits/spaces only
        var descriptor = SanitiseDescriptor(request.MerchantName, 22);

        var options = new PaymentIntentCreateOptions
        {
            Amount             = amountCents,
            Currency           = request.Currency.ToLower(),
            Customer           = customerId,
            PaymentMethod      = request.PaymentMethodId,
            PaymentMethodTypes = new List<string> { "card" },

            // confirm=true → single-step: create + authorise + capture
            Confirm            = true,
            CaptureMethod      = "automatic",          // immediate settlement

            // Required by Stripe when Confirm=true (needed for 3DS redirects)
            ReturnUrl          = "https://payments.talenceinformatixs.com/return",

            Description              = description,
            StatementDescriptorSuffix = descriptor,

            // Expand the latest_charge so we get the receipt URL in one call
            Expand             = new List<string> { "latest_charge" },

            Metadata = new Dictionary<string, string>
            {
                ["customer_name"]  = request.CustomerName,
                ["customer_email"] = request.CustomerEmail ?? string.Empty,
                ["merchant_name"]  = request.MerchantName,
                ["source"]         = "TalenceInformatixsDebitApi"
            }
        };

        _logger.LogDebug("Creating PaymentIntent | Amount cents: {Cents} | PaymentMethod: {Pm}",
            amountCents, request.PaymentMethodId);

        var intent = await _paymentIntents.CreateAsync(options, cancellationToken: ct);

        _logger.LogInformation(
            "PaymentIntent created | Id: {Id} | Status: {Status}",
            intent.Id, intent.Status);

        return MapToResponse(intent, request);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<string> EnsureCustomerAsync(
        DebitPaymentRequest request, CancellationToken ct)
    {
        // Try to reuse an existing Customer by e-mail to avoid duplicates
        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            var existing = await _customers.ListAsync(
                new CustomerListOptions { Email = request.CustomerEmail, Limit = 1 },
                cancellationToken: ct);

            if (existing.Data.Count > 0)
            {
                _logger.LogDebug("Found existing Stripe customer for {Email}", request.CustomerEmail);
                return existing.Data[0].Id;
            }
        }

        // Create a new Customer
        var newCustomer = await _customers.CreateAsync(
            new CustomerCreateOptions
            {
                Name  = request.CustomerName,
                Email = request.CustomerEmail,
                Metadata = new Dictionary<string, string>
                {
                    ["created_by"] = "TalenceInformatixsDebitApi"
                }
            },
            cancellationToken: ct);

        _logger.LogInformation("Created new Stripe Customer: {Id} for {Name}",
            newCustomer.Id, request.CustomerName);

        return newCustomer.Id;
    }

    private static DebitPaymentResponse MapToResponse(PaymentIntent intent, DebitPaymentRequest request)
    {
        var succeeded      = intent.Status == "succeeded";
        var requiresAction = intent.Status == "requires_action";
        var charge         = intent.LatestCharge;   // expanded above

        return new DebitPaymentResponse
        {
            Success        = succeeded,
            TransactionId  = intent.Id,
            ChargeId       = charge?.Id ?? string.Empty,
            Status         = intent.Status,
            AmountCharged  = intent.Amount / 100m,
            Currency       = intent.Currency.ToUpper(),
            CustomerName   = request.CustomerName,
            MerchantName   = request.MerchantName,
            Timestamp      = DateTime.UtcNow,
            ReceiptUrl     = charge?.ReceiptUrl,
            RequiresAction = requiresAction,
            ClientSecret   = requiresAction ? intent.ClientSecret : null,
            Message        = succeeded
                ? $"Payment of {intent.Amount / 100m:C} successfully authorised and captured."
                : requiresAction
                    ? "Additional authentication required. Use the clientSecret to complete 3-D Secure."
                    : $"Payment could not be completed. Status: {intent.Status}."
        };
    }

    /// <summary>
    /// Strips characters Stripe disallows in statement descriptors and trims to maxLen.
    /// Allowed: letters, digits, spaces. Special chars removed.
    /// </summary>
    private static string SanitiseDescriptor(string input, int maxLen)
    {
        var clean = new string(input
            .Where(c => char.IsLetterOrDigit(c) || c == ' ')
            .ToArray())
            .Trim();

        return clean.Length > maxLen ? clean[..maxLen] : clean;
    }
}
