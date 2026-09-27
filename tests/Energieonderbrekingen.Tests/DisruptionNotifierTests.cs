using Energieonderbrekingen.Web.Api;
using Energieonderbrekingen.Web.Data;
using Energieonderbrekingen.Web.Email;
using Energieonderbrekingen.Web.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Energieonderbrekingen.Tests;

public sealed class DisruptionNotifierTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 9, 27, 16, 0, 0, TimeSpan.FromHours(2));

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly FakeClient _client = new();
    private readonly FakeSender _sender = new();

    public DisruptionNotifierTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.Migrate();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private DisruptionNotifier CreateNotifier() => new(
        _db, _client, _sender, Options.Create(new AppOptions { PublicBaseUrl = "https://example.test" }),
        new FixedTimeProvider(Now), NullLogger<DisruptionNotifier>.Instance);

    private async Task<Subscription> SubscribeAsync(string email, string postalCode)
    {
        var s = new Subscription { Email = email, PostalCode = postalCode, UnsubscribeToken = Guid.NewGuid().ToString("N"), CreatedAt = Now };
        _db.Subscriptions.Add(s);
        await _db.SaveChangesAsync();
        return s;
    }

    private static Disruption Outage(string id, string postalCodes, DateTimeOffset? end = null, bool planned = false) => new()
    {
        Id = id,
        Planned = planned,
        Network = new() { Type = "electricity" },
        Period = new() { Begin = Now.AddHours(-1), ExpectedEnd = Now.AddHours(2), End = end },
        Location = new() { Features = new() { Properties = new() { City = "Amsterdam", PostalCode = postalCodes } } },
        Status = "monteur ter plaatse",
    };

    [Fact]
    public async Task Sends_started_once_then_resolved_once()
    {
        await SubscribeAsync("a@example.test", "4321AB");
        _client.Disruptions = [Outage("d1", "4321AB;4321AC")];

        Assert.Equal(1, await CreateNotifier().RunOnceAsync(default));
        Assert.Equal(0, await CreateNotifier().RunOnceAsync(default));

        _client.Disruptions = [Outage("d1", "4321AB;4321AC", end: Now)];
        Assert.Equal(1, await CreateNotifier().RunOnceAsync(default));
        Assert.Equal(0, await CreateNotifier().RunOnceAsync(default));

        Assert.Collection(_sender.Sent,
            m => Assert.StartsWith("Stroomstoring", m.Subject),
            m => Assert.StartsWith("Opgelost", m.Subject));
        Assert.All(_sender.Sent, m => Assert.Contains("https://example.test/afmelden?token=", m.TextBody));
    }

    [Fact]
    public async Task Only_notifies_matching_postal_codes()
    {
        await SubscribeAsync("match@example.test", "4321AB");
        await SubscribeAsync("other@example.test", "4321ZZ");
        await SubscribeAsync("pc4@example.test", "4321");
        _client.Disruptions = [Outage("d1", "4321AB")];

        Assert.Equal(2, await CreateNotifier().RunOnceAsync(default));
        Assert.Equal(["match@example.test", "pc4@example.test"], _sender.Sent.Select(m => m.To).Order());
        Assert.Equal(["4321"], _client.Requested.Distinct()); // één call per PC4
    }

    [Fact]
    public async Task Does_not_send_resolved_without_prior_started()
    {
        await SubscribeAsync("a@example.test", "4321AB");
        _client.Disruptions = [Outage("d1", "4321AB", end: Now)];

        Assert.Equal(0, await CreateNotifier().RunOnceAsync(default));
    }

    [Fact]
    public async Task Planned_interruption_uses_planned_subject()
    {
        await SubscribeAsync("a@example.test", "3011AA");
        var planned = Outage("p1", "3011", planned: true);
        planned.Network = new() { Type = "gas" };
        planned.Period = new() { PlannedBegin = Now.AddDays(7), PlannedEnd = Now.AddDays(7).AddHours(4) };
        _client.Disruptions = [planned];

        Assert.Equal(1, await CreateNotifier().RunOnceAsync(default));
        Assert.StartsWith("Geplande gasonderbreking", Assert.Single(_sender.Sent).Subject);
    }

    [Fact]
    public async Task Failed_send_is_retried_next_run()
    {
        await SubscribeAsync("a@example.test", "4321AB");
        _client.Disruptions = [Outage("d1", "4321AB")];

        _sender.Fail = true;
        Assert.Equal(0, await CreateNotifier().RunOnceAsync(default));

        _sender.Fail = false;
        Assert.Equal(1, await CreateNotifier().RunOnceAsync(default));
    }

    [Fact]
    public async Task Api_timeout_skips_only_that_pc4()
    {
        await SubscribeAsync("slow@example.test", "1111AA");
        await SubscribeAsync("ok@example.test", "4321AB");
        _client.Disruptions = [Outage("d1", "1111AA;4321AB")];
        _client.TimeoutFor = "1111";

        // HttpClient-time-out gooit TaskCanceledException terwijl onze token niet geannuleerd is.
        Assert.Equal(1, await CreateNotifier().RunOnceAsync(default));
        Assert.Equal("ok@example.test", Assert.Single(_sender.Sent).To);
    }

    [Fact]
    public async Task Cancellation_on_shutdown_is_propagated()
    {
        await SubscribeAsync("a@example.test", "4321AB");
        _client.TimeoutFor = "4321";

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateNotifier().RunOnceAsync(new CancellationToken(canceled: true)));
    }

    [Fact]
    public async Task Unsubscribe_during_poll_does_not_abort_other_mails()
    {
        await SubscribeAsync("gone@example.test", "4321AB");
        await SubscribeAsync("stays@example.test", "4321AB");
        _client.Disruptions = [Outage("d1", "4321AB")];
        _sender.OnSend = async m =>
        {
            if (m.To == "gone@example.test")
                await _db.Subscriptions.Where(s => s.Email == m.To).ExecuteDeleteAsync();
        };

        Assert.Equal(2, await CreateNotifier().RunOnceAsync(default));
        Assert.Equal(["gone@example.test", "stays@example.test"], _sender.Sent.Select(m => m.To).Order());
        Assert.Single(await _db.NotificationLogs.ToListAsync());
    }

    private sealed class FakeClient : IDisruptionClient
    {
        public List<Disruption> Disruptions { get; set; } = [];
        public List<string> Requested { get; } = [];
        public string? TimeoutFor { get; set; }

        public Task<IReadOnlyList<Disruption>> GetDisruptionsAsync(string postalCode, CancellationToken cancellationToken)
        {
            Requested.Add(postalCode);
            if (postalCode == TimeoutFor)
                throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout");
            return Task.FromResult<IReadOnlyList<Disruption>>(Disruptions);
        }
    }

    private sealed class FakeSender : IEmailSender
    {
        public bool Fail { get; set; }
        public Func<EmailMessage, Task>? OnSend { get; set; }
        public List<EmailMessage> Sent { get; } = [];

        public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (Fail)
                throw new InvalidOperationException("SMTP down");
            Sent.Add(message);
            if (OnSend is not null)
                await OnSend(message);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
    }
}
