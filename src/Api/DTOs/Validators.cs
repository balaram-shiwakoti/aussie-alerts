using FluentValidation;
namespace AussieAlerts.DTOs;

public static class InputRules
{
    public static readonly string[] Types = ["house", "unit", "townhouse", "land"];
    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();
}
public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128)
            .Matches("[a-z]").Matches("[A-Z]").Matches("[0-9]");
    }
}
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
public sealed class AlertValidator : AbstractValidator<AlertRequest>
{
    public AlertValidator()
    {
        RuleFor(x => x).Must(x => !string.IsNullOrWhiteSpace(x.Suburb) || !string.IsNullOrWhiteSpace(x.Postcode))
            .WithMessage("Enter a suburb or postcode.");
        RuleFor(x => x.Suburb).MaximumLength(80);
        RuleFor(x => x.Postcode).Matches("^[0-9]{4}$").When(x => !string.IsNullOrEmpty(x.Postcode));
        RuleFor(x => x.MinPrice).InclusiveBetween(0, 100_000_000);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(x => x.MinPrice).LessThanOrEqualTo(100_000_000);
        RuleFor(x => x.MinBedrooms).InclusiveBetween(0, 20);
        RuleFor(x => x.PropertyType).Must(x => InputRules.Types.Contains(x!)).When(x => !string.IsNullOrEmpty(x.PropertyType));
    }
}
public sealed class ListingValidator : AbstractValidator<ListingRequest>
{
    public ListingValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Suburb).NotEmpty().MaximumLength(80);
        RuleFor(x => x.Postcode).NotEmpty().Matches("^[0-9]{4}$");
        RuleFor(x => x.Price).InclusiveBetween(0, 100_000_000);
        RuleFor(x => x.Bedrooms).InclusiveBetween(0, 20);
        RuleFor(x => x.PropertyType).Must(x => InputRules.Types.Contains(x));
    }
}
public sealed class ListingQueryValidator : AbstractValidator<ListingQuery>
{
    public ListingQueryValidator()
    {
        RuleFor(x => x.Postcode).Matches("^[0-9]{4}$").When(x => x.Postcode != null);
        RuleFor(x => x.MinPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x.MaxPrice).GreaterThanOrEqualTo(0);
        RuleFor(x => x).Must(x => !x.MinPrice.HasValue || !x.MaxPrice.HasValue || x.MinPrice <= x.MaxPrice)
            .WithMessage("Maximum price must be at least minimum price.");
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class PageValidator : AbstractValidator<PageQuery>
{
    public PageValidator()
    {
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
