using System.Security.Claims;
using AussieAlerts.Data;
using AussieAlerts.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Controllers;

[ApiController, Route("api/notifications"), Authorize]
public sealed class NotificationsController(AppDb db) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet]
    public async Task<ActionResult<NotificationPage>> List([FromQuery] PageQuery request,
        IValidator<PageQuery> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var query = db.Notifications.AsNoTracking().Where(x => x.UserId == Owner);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new NotificationDto(x.Id, x.AlertId, x.ListingId, x.Subject, x.Body, x.CreatedAt, x.ReadAt)).ToListAsync(ct);
        return Ok(new NotificationPage(items, total, request.Page, request.PageSize));
    }
    [HttpPut("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var count = await db.Notifications.Where(x => x.Id == id && x.UserId == Owner)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ReadAt, x => x.ReadAt ?? DateTime.UtcNow), ct);
        return count == 0 ? NotFound() : NoContent();
    }
}
