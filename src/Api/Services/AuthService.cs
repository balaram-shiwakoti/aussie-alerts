using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AussieAlerts.Data;
using AussieAlerts.Domain;
using AussieAlerts.DTOs;
using AussieAlerts.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
namespace AussieAlerts.Services;

public sealed class AuthService(AppDb db, IPasswordHasher<AppUser> hasher, IConfiguration config)
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var user = new AppUser { Email = request.Email.Trim().ToLowerInvariant() };
        user.PasswordHash = hasher.HashPassword(user, request.Password);
        db.Users.Add(user); // Public registration can NEVER choose a role.
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        { throw new ApiException(409, "An account already uses this email."); }
        return Issue(user);
    }
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await db.Users.SingleOrDefaultAsync(x => x.Email == email, ct);
        var result = user == null ? PasswordVerificationResult.Failed : hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
        if (user == null || result == PasswordVerificationResult.Failed) throw new ApiException(401, "Invalid email or password.");
        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(ct);
        }
        return Issue(user);
    }
    private AuthResponse Issue(AppUser user)
    {
        var expires = DateTime.UtcNow.AddMinutes(30);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var token = new JwtSecurityToken(config["Jwt:Issuer"], config["Jwt:Audience"],
            [new Claim("sub", user.Id.ToString()), new Claim("role", user.Role)],
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires, user.Email, user.Role);
    }
}
