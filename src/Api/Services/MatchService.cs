using AussieAlerts.Data;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Services;

public sealed class MatchService(AppDb db)
{
    // One atomic statement: retries, concurrent workers and restarts cannot duplicate an alert/listing pair.
    // No moving watermark: late commits are revisited on the next poll.
    public Task<int> RunAsync(CancellationToken ct = default) => db.Database.ExecuteSqlRawAsync("""
        INSERT INTO "Notifications" ("Id", "UserId", "AlertId", "ListingId", "Subject", "Body", "CreatedAt")
        SELECT gen_random_uuid(), a."UserId", a."Id", l."Id",
               'New fake listing: ' || l."Title",
               'SIMULATED EMAIL ONLY. ' || l."Suburb" || ' ' || l."Postcode" ||
               ', AUD ' || l."Price"::text || ', ' || l."Bedrooms"::text ||
               ' bedrooms, ' || l."PropertyType" || '. No real property or email delivery.',
               clock_timestamp()
        FROM "Alerts" a JOIN "Listings" l
          ON l."CreatedAt" >= a."ActiveSince"
         AND (a."Postcode" IS NULL OR a."Postcode" = l."Postcode")
         AND (a."Suburb" IS NULL OR a."Suburb" = l."Suburb")
         AND l."Price" BETWEEN a."MinPrice" AND a."MaxPrice"
         AND l."Bedrooms" >= a."MinBedrooms"
         AND (a."PropertyType" IS NULL OR a."PropertyType" = l."PropertyType")
        WHERE a."Active" = TRUE
          AND NOT EXISTS (SELECT 1 FROM "Notifications" n
                          WHERE n."AlertId" = a."Id" AND n."ListingId" = l."Id")
        ON CONFLICT ("AlertId", "ListingId") DO NOTHING;
        """, ct);
}

public sealed class MatchingWorker(IServiceScopeFactory scopes, IConfiguration config,
    ILogger<MatchingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Matching:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(config.GetValue("Matching:IntervalSeconds", 15)));
        try
        {
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var count = await scope.ServiceProvider.GetRequiredService<MatchService>().RunAsync(stoppingToken);
                    if (count > 0) logger.LogInformation("Created {Count} simulated notifications", count);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogError(ex, "Matching failed; next scheduled poll will retry"); }
            } while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
