using Energieonderbrekingen.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Energieonderbrekingen.Web.Pages;

// POST zonder antiforgery-token is nodig voor one-click unsubscribe (RFC 8058) vanuit mailclients.
[IgnoreAntiforgeryToken]
public class AfmeldenModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    public string? PostalCode { get; private set; }
    public bool Unsubscribed { get; private set; }
    public bool InvalidToken { get; private set; }

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var subscription = await FindAsync(cancellationToken);
        if (subscription is null)
            InvalidToken = true;
        else
            PostalCode = subscription.PostalCode;
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var subscription = await FindAsync(cancellationToken);
        if (subscription is not null)
        {
            PostalCode = subscription.PostalCode;
            db.Subscriptions.Remove(subscription);
            await db.SaveChangesAsync(cancellationToken);
        }

        Unsubscribed = true;
        return Page();
    }

    private Task<Subscription?> FindAsync(CancellationToken cancellationToken) =>
        string.IsNullOrWhiteSpace(Token)
            ? Task.FromResult<Subscription?>(null)
            : db.Subscriptions.FirstOrDefaultAsync(s => s.UnsubscribeToken == Token, cancellationToken);
}
