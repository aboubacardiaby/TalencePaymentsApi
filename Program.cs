using Serilog;
using Stripe;
using TalenceInformatixs.Payments.Api.Configuration;
using TalenceInformatixs.Payments.Api.Middleware;
using TalenceInformatixs.Payments.Api.Services;

// ─── Serilog bootstrap ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog (full) ───────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/talence-payments-.log",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 30));

    // ─── Configuration ────────────────────────────────────────────────────────
    builder.Services.Configure<StripeSettings>(
        builder.Configuration.GetSection(StripeSettings.SectionName));

    // ─── Stripe SDK ───────────────────────────────────────────────────────────
    var stripeKey = builder.Configuration["Stripe:SecretKey"]
        ?? throw new InvalidOperationException("Stripe:SecretKey is not configured.");
    StripeConfiguration.ApiKey = stripeKey;

    // ─── Application services ─────────────────────────────────────────────────
    builder.Services.AddSingleton<CustomerService>();
    builder.Services.AddSingleton<PaymentIntentService>();
    builder.Services.AddScoped<IStripePaymentService, StripePaymentService>();

    // ─── Controllers ──────────────────────────────────────────────────────────
    builder.Services.AddControllers();

    // ─── CORS ─────────────────────────────────────────────────────────────────
    builder.Services.AddCors(o => o.AddPolicy("AllowAll", p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));

    // ─── Swagger / OpenAPI ────────────────────────────────────────────────────
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new()
        {
            Title       = "Talence Informatixs – Debit Payment API",
            Version     = "v1",
            Description = "Real-time debit-card payment processing powered by Stripe.\n\n" +
                          "**Test card:** `4000056655665556` (Visa Debit) · exp any future date · CVC any 3 digits\n\n" +
                          "Use the `GET /api/payments/debit/sample-request` endpoint to fetch a ready-made " +
                          "request body for the **David Johnson → Talence Informatixs Inc** scenario.",
            Contact = new() { Name = "Talence Informatixs Inc", Email = "payments@talenceinformatixs.com" }
        });

        var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        if (System.IO.File.Exists(xmlPath))
            c.IncludeXmlComments(xmlPath);
    });

    // ─── Health checks ────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks();

    // ─────────────────────────────────────────────────────────────────────────
    var app = builder.Build();
    // ─────────────────────────────────────────────────────────────────────────

    app.UseSerilogRequestLogging();

    //if (app.Environment.IsDevelopment()|| app.Environment.IsProduction == true)
    //{
        app.UseSwagger();
        app.UseSwaggerUI(c =>
        {
            c.SwaggerEndpoint("/swagger/v1/swagger.json",
                              "Talence Informatixs Debit Payment API v1");
            c.RoutePrefix = string.Empty;   // Swagger at root "/"
            c.DocumentTitle = "Talence Payments API";
        });
  //  }

    app.UseMiddleware<GlobalExceptionMiddleware>();
    app.UseCors("AllowAll");
    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();
    app.MapHealthChecks("/health");

    Log.Information("🚀 Talence Informatixs Payments API starting on {Env}",
                    app.Environment.EnvironmentName);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application startup failed");
}
finally
{
    Log.CloseAndFlush();
}
