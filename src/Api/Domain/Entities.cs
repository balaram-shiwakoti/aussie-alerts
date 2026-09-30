namespace AussieAlerts.Domain;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "User";
}

public sealed class Listing
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Suburb { get; set; } = "";
    public string Postcode { get; set; } = "";
    public decimal Price { get; set; }
    public int Bedrooms { get; set; }
    public string PropertyType { get; set; } = "house";
    public DateTime CreatedAt { get; set; }
}

public sealed class PropertyAlert
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string? Suburb { get; set; }
    public string? Postcode { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    public int MinBedrooms { get; set; }
    public string? PropertyType { get; set; }
    public bool Active { get; set; } = true;
    public DateTime ActiveSince { get; set; }
}

public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid AlertId { get; set; }
    public Guid ListingId { get; set; }
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
