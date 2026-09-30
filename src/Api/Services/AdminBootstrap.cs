using FluentValidation;
using AussieAlerts.Data;
using AussieAlerts.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace AussieAlerts.Services;

public static class AdminBootstrap
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration config)
    {
        var email = config["Seed:AdminEmail"];
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Seed admin credentials are required when bootstrap is enabled.");
        await new DTOs.RegisterValidator().ValidateAndThrowAsync(new(email, password));
        var db = services.GetRequiredService<AppDb>();
        email = email.Trim().ToLowerInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(x => x.Email == email);
        if (existing != null)
        {
            if (existing.Role != "Admin") throw new InvalidOperationException("Bootstrap email belongs to a non-admin; refusing to promote it.");
            return;
        }
        var user = new AppUser { Email = email, Role = "Admin" };
        user.PasswordHash = services.GetRequiredService<IPasswordHasher<AppUser>>().HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync();
    }
}
