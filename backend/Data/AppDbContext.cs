using FaceAccessController.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FaceAccessController.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Employee> Employees => Set<Employee>();
    public DbSet<FaceDevice> FaceDevices => Set<FaceDevice>();
    public DbSet<EmployeeFaceDevice> EmployeeFaceDevices => Set<EmployeeFaceDevice>();
    public DbSet<FaceTransaction> Transactions => Set<FaceTransaction>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>().HasIndex(x => x.Username).IsUnique();
        modelBuilder.Entity<Employee>().HasIndex(x => x.PersonId).IsUnique();
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.FaceDevice)
            .WithMany()
            .HasForeignKey(e => e.FaceDeviceId)
            .OnDelete(DeleteBehavior.SetNull);
        modelBuilder.Entity<EmployeeFaceDevice>()
            .HasIndex(x => new { x.EmployeeId, x.FaceDeviceId })
            .IsUnique();
        modelBuilder.Entity<EmployeeFaceDevice>()
            .HasOne(x => x.Employee)
            .WithMany(e => e.FaceDeviceAccess)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<EmployeeFaceDevice>()
            .HasOne(x => x.FaceDevice)
            .WithMany()
            .HasForeignKey(x => x.FaceDeviceId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<FaceTransaction>()
            .HasIndex(x => new { x.PersonId, x.TransactionTime, x.DeviceSn })
            .IsUnique();
        // Speeds date-range report queries (daily / monthly / hours).
        modelBuilder.Entity<FaceTransaction>().HasIndex(x => x.TransactionTime);
    }
}
