using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Energieonderbrekingen.Web.Api;
using Energieonderbrekingen.Web.Data;
using Energieonderbrekingen.Web.Email;
using Energieonderbrekingen.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Energieonderbrekingen.Web.Pages;

[EnableRateLimiting("subscribe")]
public class IndexModel(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<AppOptions> appOptions,
    TimeProvider timeProvider,
    ILogger<IndexModel> logger) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Vul je postcode in.")]
    [Display(Name = "Postcode")]
    public string PostalCodeInput { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Vul je e-mailadres in.")]
    [EmailAddress(ErrorMessage = "Dit is geen geldig e-mailadres.")]
    [StringLength(254)]
    [Display(Name = "E-mailadres")]
    public string Email { get; set; } = "";

    /// <summary>Honeypot: onzichtbaar veld dat alleen bots invullen.</summary>
    [BindProperty]
    public string? Website { get; set; }

    public string? SubscribedPostalCode { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!PostalCode.IsValid(PostalCodeInput))
            ModelState.AddModelError(nameof(PostalCodeInput), "Vul een geldige postcode in, bijvoorbeeld 1234 AB.");

        if (!ModelState.IsValid)
            return Page();

        var postalCode = PostalCode.Normalize(PostalCodeInput);
        var email = Email.Trim().ToLowerInvariant();

        if (!string.IsNullOrEmpty(Website))
        {
            logger.LogInformation("Honeypot ingevuld, aanmelding genegeerd");
            SubscribedPostalCode = postalCode;
            return Page();
        }

        var exists = await db.Subscriptions.AnyAsync(s => s.Email == email && s.PostalCode == postalCode, cancellationToken);
        if (!exists)
        {
            var subscription = new Subscription
            {
                Email = email,
                PostalCode = postalCode,
                UnsubscribeToken = Base64UrlToken(),
                CreatedAt = timeProvider.GetUtcNow(),
            };
            db.Subscriptions.Add(subscription);

            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException)
            {
                // Gelijktijdige dubbele aanmelding: unieke index vangt het af, voor de gebruiker is het resultaat hetzelfde.
                SubscribedPostalCode = postalCode;
                return Page();
            }

            try
            {
                var unsubscribeUrl = appOptions.Value.UnsubscribeUrl(subscription.UnsubscribeToken);
                await emailSender.SendAsync(EmailTemplates.Welcome(email, postalCode, unsubscribeUrl), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Welkomstmail voor abonnement {Id} kon niet worden verstuurd", subscription.Id);
            }
        }

        SubscribedPostalCode = postalCode;
        ModelState.Clear();
        PostalCodeInput = "";
        Email = "";
        return Page();
    }

    private static string Base64UrlToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
