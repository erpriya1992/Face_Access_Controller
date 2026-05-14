using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Services;

public class DataSeeder(AppDbContext db, IConfiguration configuration)
{
    public async Task SeedAsync()
    {
        if (await db.Users.AnyAsync())
        {
            return;
        }

        var username = configuration["Admin:Username"] ?? "admin";
        var password = configuration["Admin:Password"] ?? "Admin@123";

        db.Users.Add(new AppUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = "Admin"
        });

        await db.SaveChangesAsync();
    }
}
