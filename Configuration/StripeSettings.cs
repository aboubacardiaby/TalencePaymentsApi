namespace TalenceInformatixs.Payments.Api.Configuration;

/// <summary>Strongly-typed Stripe configuration bound from appsettings.json.</summary>
public sealed class StripeSettings
{
    public const string SectionName = "Stripe";

    public string SecretKey      { get; set; } = string.Empty;
    public string WebhookSecret  { get; set; } = string.Empty;
    public string PublishableKey { get; set; } = string.Empty;
}
