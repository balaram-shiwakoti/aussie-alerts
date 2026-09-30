using AussieAlerts.Domain;
using AussieAlerts.DTOs;
using AussieAlerts.Infrastructure;
using AussieAlerts.Repositories;
namespace AussieAlerts.Services;

public sealed class AlertService(AlertRepository repo)
{
    public async Task<List<AlertDto>> ListAsync(Guid owner, CancellationToken ct) =>
        (await repo.ListAsync(owner, ct)).Select(Map).ToList();
    public async Task<AlertDto> CreateAsync(Guid owner, AlertRequest request, CancellationToken ct)
    {
        var alert = new PropertyAlert { UserId = owner };
        Apply(alert, request);
        repo.Add(alert); // Database generates ActiveSince using its own UTC clock.
        await repo.SaveAsync(ct);
        return Map(alert);
    }
    public async Task<AlertDto> UpdateAsync(Guid owner, Guid id, AlertRequest request, CancellationToken ct)
    {
        var alert = await repo.FindAsync(owner, id, ct) ?? throw new ApiException(404, "Alert not found.");
        Apply(alert, request);
        // Editing starts a new matching window; old notifications are retained.
        alert.ActiveSince = await repo.NowAsync(ct);
        await repo.SaveAsync(ct);
        return Map(alert);
    }
    private static void Apply(PropertyAlert a, AlertRequest r)
    {
        a.Suburb = InputRules.Normalize(r.Suburb); a.Postcode = InputRules.Normalize(r.Postcode);
        a.MinPrice = r.MinPrice; a.MaxPrice = r.MaxPrice; a.MinBedrooms = r.MinBedrooms;
        a.PropertyType = InputRules.Normalize(r.PropertyType); a.Active = r.Active;
    }
    private static AlertDto Map(PropertyAlert a) => new(a.Id, a.Suburb, a.Postcode,
        a.MinPrice, a.MaxPrice, a.MinBedrooms, a.PropertyType, a.Active, a.ActiveSince);
}

public static class AlertMatcher
{
    // Reference rule used by unit tests; the bulk SQL implementation has database integration tests.
    public static bool Matches(PropertyAlert a, Listing l) => a.Active && l.CreatedAt >= a.ActiveSince
        && (a.Suburb == null || a.Suburb == l.Suburb)
        && (a.Postcode == null || a.Postcode == l.Postcode)
        && l.Price >= a.MinPrice && l.Price <= a.MaxPrice
        && l.Bedrooms >= a.MinBedrooms && (a.PropertyType == null || a.PropertyType == l.PropertyType);
}
