using System.Security.Claims;
using AussieAlerts.DTOs;
using AussieAlerts.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace AussieAlerts.Controllers;

[ApiController, Route("api/alerts"), Authorize]
public sealed class AlertsController(AlertService service) : ControllerBase
{
    private Guid Owner => Guid.Parse(User.FindFirstValue("sub")!);
    [HttpGet]
    public Task<List<AlertDto>> List(CancellationToken ct) => service.ListAsync(Owner, ct);
    [HttpPost]
    public async Task<ActionResult<AlertDto>> Create(AlertRequest request, IValidator<AlertRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Ok(await service.CreateAsync(Owner, request, ct));
    }
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AlertDto>> Update(Guid id, AlertRequest request, IValidator<AlertRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        return Ok(await service.UpdateAsync(Owner, id, request, ct));
    }
}
