using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using TalenceInformatixs.Payments.Api.Configuration;

namespace TalenceInformatixs.Payments.Api.Controllers;

/// <summary>Health and readiness probes for the Payments API.</summary>
[ApiController]
[Route("api/[controller]")]
public sealed class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly StripeSettings _settings;

    public HealthController(
        ILogger<HealthController> logger,
        IOptions<StripeSettings> settings)
    {
        _logger   = logger;
        _settings = settings.Value;
    }

    /// <summary>Liveness probe – confirms the API is running.</summary>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Live() =>
        Ok(new
        {
            status    = "alive",
            service   = "Talence Informatixs Debit Payment API",
            version   = "1.0.0",
            timestamp = DateTime.UtcNow
        });

    /// <summary>
    /// Readiness probe – verifies connectivity to the Stripe API.
    /// Returns 503 if Stripe is unreachable.
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready()
    {
        try
        {
            // A lightweight Stripe call to verify the secret key is valid
            var svc     = new BalanceService();
            var balance = await svc.GetAsync();

            return Ok(new
            {
                status    = "ready",
                stripe    = "connected",
                available = balance.Available.Select(b => new
                {
                    amount   = b.Amount / 100m,
                    currency = b.Currency.ToUpper()
                }),
                timestamp = DateTime.UtcNow
            });
        }
        catch (StripeException ex)
        {
            _logger.LogError(ex, "Stripe connectivity check failed during readiness probe.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                status    = "degraded",
                stripe    = "unreachable",
                error     = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}
