using AussieAlerts.Data;
using AussieAlerts.Domain;
using AussieAlerts.DTOs;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Controllers;

[ApiController, Route("api/listings"), Authorize]
public sealed class ListingsController(AppDb db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ListingPage>> Browse([FromQuery] ListingQuery request,
        IValidator<ListingQuery> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var query = db.Listings.AsNoTracking().AsQueryable();
        if (request.Postcode != null) query = query.Where(x => x.Postcode == request.Postcode);
        if (request.MinPrice != null) query = query.Where(x => x.Price >= request.MinPrice);
        if (request.MaxPrice != null) query = query.Where(x => x.Price <= request.MaxPrice);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(x => new ListingDto(x.Id, x.Title, x.Suburb, x.Postcode, x.Price,
                x.Bedrooms, x.PropertyType, x.CreatedAt)).ToListAsync(ct);
        return Ok(new ListingPage(items, total, request.Page, request.PageSize));
    }
}

[ApiController, Route("api/admin/listings"), Authorize(Roles = "Admin")]
public sealed class AdminController(AppDb db) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<ListingDto>> Create(ListingRequest request, IValidator<ListingRequest> validator, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(request, ct);
        var listing = new Listing { Title = request.Title.Trim(), Suburb = InputRules.Normalize(request.Suburb)!,
            Postcode = request.Postcode, Price = request.Price, Bedrooms = request.Bedrooms,
            PropertyType = request.PropertyType };
        db.Listings.Add(listing);
        await db.SaveChangesAsync(ct);
        return Ok(new ListingDto(listing.Id, listing.Title, listing.Suburb, listing.Postcode,
            listing.Price, listing.Bedrooms, listing.PropertyType, listing.CreatedAt));
    }
}
