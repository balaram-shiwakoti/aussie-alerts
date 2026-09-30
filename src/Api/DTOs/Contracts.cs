namespace AussieAlerts.DTOs;

public sealed record RegisterRequest(string Email, string Password);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt, string Email, string Role);
public sealed record AlertRequest(string? Suburb, string? Postcode, decimal MinPrice,
    decimal MaxPrice, int MinBedrooms, string? PropertyType, bool Active);
public sealed record AlertDto(Guid Id, string? Suburb, string? Postcode, decimal MinPrice,
    decimal MaxPrice, int MinBedrooms, string? PropertyType, bool Active, DateTime ActiveSince);
public sealed record ListingRequest(string Title, string Suburb, string Postcode,
    decimal Price, int Bedrooms, string PropertyType);
public sealed record ListingDto(Guid Id, string Title, string Suburb, string Postcode,
    decimal Price, int Bedrooms, string PropertyType, DateTime CreatedAt);
public sealed record NotificationDto(Guid Id, Guid AlertId, Guid ListingId,
    string Subject, string Body, DateTime CreatedAt, DateTime? ReadAt);
public sealed record ListingPage(List<ListingDto> Items, int Total, int Page, int PageSize);
public sealed record NotificationPage(List<NotificationDto> Items, int Total, int Page, int PageSize);
public sealed record ListingQuery(string? Postcode = null, decimal? MinPrice = null,
    decimal? MaxPrice = null, int Page = 1, int PageSize = 20);
public sealed record PageQuery(int Page = 1, int PageSize = 20);
