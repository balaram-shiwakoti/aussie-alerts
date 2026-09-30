using AussieAlerts.Domain;
using AussieAlerts.Services;
using AussieAlerts.DTOs;
namespace Api.Tests;
public sealed class MatchingTests
{
    [Theory]
    [InlineData(100, 2, true)]
    [InlineData(200, 3, true)]
    [InlineData(99, 2, false)]
    [InlineData(201, 2, false)]
    [InlineData(150, 1, false)]
    public void Price_is_inclusive_and_bedrooms_is_minimum(int price, int beds, bool expected)
    {
        var start = DateTime.UtcNow;
        var alert = new PropertyAlert { Postcode="0800", MinPrice=100, MaxPrice=200, MinBedrooms=2, ActiveSince=start };
        var listing = new Listing { Postcode="0800", Price=price, Bedrooms=beds, CreatedAt=start.AddSeconds(1) };
        Assert.Equal(expected, AlertMatcher.Matches(alert, listing));
    }
    [Fact]
    public void Both_locations_type_active_and_future_time_are_required()
    {
        var start=DateTime.UtcNow;
        var a = new PropertyAlert { Suburb="darwin", Postcode="0800", PropertyType="unit", MaxPrice=1000, ActiveSince=start };
        var l = new Listing { Suburb="darwin", Postcode="0800", PropertyType="unit", Price=500, CreatedAt=start.AddSeconds(1) };
        Assert.True(AlertMatcher.Matches(a,l));
        l.Suburb="other"; Assert.False(AlertMatcher.Matches(a,l)); l.Suburb="darwin";
        l.Postcode="2000"; Assert.False(AlertMatcher.Matches(a,l)); l.Postcode="0800";
        l.PropertyType="house"; Assert.False(AlertMatcher.Matches(a,l)); l.PropertyType="unit";
        l.CreatedAt=start.AddSeconds(-1); Assert.False(AlertMatcher.Matches(a,l)); l.CreatedAt=start.AddSeconds(1);
        a.Active=false; Assert.False(AlertMatcher.Matches(a,l));
    }
    [Fact]
    public void Validation_preserves_leading_zero_and_rejects_empty_location()
    {
        var v = new AlertValidator();
        Assert.True(v.Validate(new AlertRequest(null,"0800",0,200,2,null,true)).IsValid);
        Assert.False(v.Validate(new AlertRequest(null,null,0,200,2,null,true)).IsValid);
        Assert.False(v.Validate(new AlertRequest(null,"800",0,200,2,null,true)).IsValid);
        Assert.False(v.Validate(new AlertRequest(null,"0800",300,200,2,null,true)).IsValid);
    }
}
