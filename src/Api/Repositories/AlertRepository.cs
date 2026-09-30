using AussieAlerts.Data;
using AussieAlerts.Domain;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Repositories;

// A focused repository for owner-scoped alert access. DbContext already acts as a unit of work.
public sealed class AlertRepository(AppDb db)
{
    public Task<List<PropertyAlert>> ListAsync(Guid owner, CancellationToken ct) =>
        db.Alerts.AsNoTracking().Where(x => x.UserId == owner).OrderByDescending(x => x.ActiveSince).ToListAsync(ct);
    public Task<PropertyAlert?> FindAsync(Guid owner, Guid id, CancellationToken ct) =>
        db.Alerts.SingleOrDefaultAsync(x => x.Id == id && x.UserId == owner, ct);
    public void Add(PropertyAlert alert) => db.Alerts.Add(alert);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public Task<DateTime> NowAsync(CancellationToken ct) =>
        db.Database.SqlQuery<DateTime>($"SELECT clock_timestamp() AS \"Value\"").SingleAsync(ct);
}
