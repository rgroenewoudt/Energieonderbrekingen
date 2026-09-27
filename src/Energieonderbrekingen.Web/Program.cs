using System.Threading.RateLimiting;
using Energieonderbrekingen.Web.Api;
using Energieonderbrekingen.Web.Data;
using Energieonderbrekingen.Web.Email;
using Energieonderbrekingen.Web.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=energieonderbrekingen.db"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<AppOptions>(builder.Configuration.GetSection(AppOptions.Section));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.Section));
builder.Services.Configure<EnergieonderbrekingenOptions>(builder.Configuration.GetSection(EnergieonderbrekingenOptions.Section));

// Echte API alleen als mock uit staat én er credentials zijn.
var apiOptions = builder.Configuration.GetSection(EnergieonderbrekingenOptions.Section).Get<EnergieonderbrekingenOptions>() ?? new();
if (!apiOptions.UseMock && apiOptions.HasCredentials)
    builder.Services.AddHttpClient<IDisruptionClient, EnergieonderbrekingenClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
else
    builder.Services.AddSingleton<IDisruptionClient, MockDisruptionClient>();

// SendGrid SMTP als die geconfigureerd is, anders e-mails naar de log.
var smtpOptions = builder.Configuration.GetSection(SmtpOptions.Section).Get<SmtpOptions>() ?? new();
if (smtpOptions.IsConfigured)
    builder.Services.AddSingleton<IEmailSender, SmtpEmailSender>();
else
    builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();

builder.Services.AddScoped<DisruptionNotifier>();
builder.Services.AddHostedService<DisruptionMonitor>();

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("subscribe", ctx =>
        HttpMethods.IsPost(ctx.Request.Method)
            ? RateLimitPartition.GetFixedWindowLimiter(
                ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 5, Window = TimeSpan.FromMinutes(1) })
            : RateLimitPartition.GetNoLimiter("get"));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.Logger.LogInformation("Databron: {Client}, e-mail: {Sender}",
    app.Services.GetRequiredService<IDisruptionClient>().GetType().Name,
    app.Services.GetRequiredService<IEmailSender>().GetType().Name);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseRateLimiter();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();

public partial class Program;
