using TalenceInformatixs.Payments.Api.Models;

namespace TalenceInformatixs.Payments.Api.Services;

/// <summary>Contract for processing debit-card payments via Stripe.</summary>
public interface IStripePaymentService
{
    /// <summary>
    /// Authorises and captures a debit-card payment in real time.
    /// </summary>
    /// <param name="request">Payment details including the tokenised paymentMethodId.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<DebitPaymentResponse> ProcessDebitPaymentAsync(
        DebitPaymentRequest request,
        CancellationToken ct = default);
}
