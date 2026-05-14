using FaceAccessController.Api.Data;
using FaceAccessController.Api.Models;
using FaceAccessController.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Net;
using System.Net.Http;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSignalR();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// FaceReader_Middleware / device calls: avoid stale pooled sockets (forcible close from remote host)
// and allow slow photo/device operations. See https://learn.microsoft.com/dotnet/fundamentals/networking/http/httpclient-guidelines
builder.Services.AddHttpClient<MiddlewareClient>()
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectTimeout = TimeSpan.FromSeconds(45),
        AutomaticDecompression = DecompressionMethods.All
    })
    .ConfigureHttpClient(c =>
    {
        c.Timeout = TimeSpan.FromSeconds(90);
    });
builder.Services.AddScoped<JwtTokenService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<DataSeeder>();

var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        await db.Database.ExecuteSqlRawAsync("""
            IF COL_LENGTH('Employees', 'PhotoBase64') IS NULL
            BEGIN
                ALTER TABLE Employees ADD PhotoBase64 nvarchar(max) NULL;
            END
            IF COL_LENGTH('Employees', 'FaceId') IS NULL
            BEGIN
                ALTER TABLE Employees ADD FaceId nvarchar(256) NULL;
            END
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes i
                INNER JOIN sys.tables t ON i.object_id = t.object_id
                WHERE i.name = N'IX_Transactions_TransactionTime' AND t.name = N'Transactions')
            BEGIN
                CREATE NONCLUSTERED INDEX IX_Transactions_TransactionTime ON Transactions (TransactionTime);
            END
            IF COL_LENGTH('Employees', 'DoorAccessAllowed') IS NULL
            BEGIN
                ALTER TABLE Employees ADD DoorAccessAllowed bit NOT NULL CONSTRAINT DF_Employees_DoorAccessAllowed DEFAULT 1;
            END
            IF OBJECT_ID(N'dbo.FaceDevices', N'U') IS NULL
            BEGIN
                CREATE TABLE FaceDevices (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    Name nvarchar(200) NOT NULL,
                    DepartmentLabel nvarchar(200) NULL,
                    DeviceIp nvarchar(500) NOT NULL,
                    IsActive bit NOT NULL CONSTRAINT DF_FaceDevices_IsActive DEFAULT (1),
                    SortOrder int NOT NULL CONSTRAINT DF_FaceDevices_SortOrder DEFAULT (0)
                );
            END
            IF COL_LENGTH('Employees', 'FaceDeviceId') IS NULL
            BEGIN
                ALTER TABLE Employees ADD FaceDeviceId int NULL;
            END
            IF COL_LENGTH('FaceDevices', 'Description') IS NULL
            BEGIN
                ALTER TABLE FaceDevices ADD Description nvarchar(500) NULL;
            END
            IF COL_LENGTH('FaceDevices', 'DevicePassword') IS NULL
            BEGIN
                ALTER TABLE FaceDevices ADD DevicePassword nvarchar(200) NULL;
            END
            IF COL_LENGTH('FaceDevices', 'SettingsJson') IS NULL
            BEGIN
                ALTER TABLE FaceDevices ADD SettingsJson nvarchar(max) NULL;
            END
            IF OBJECT_ID(N'dbo.EmployeeFaceDevices', N'U') IS NULL
            BEGIN
                CREATE TABLE EmployeeFaceDevices (
                    Id int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    EmployeeId int NOT NULL,
                    FaceDeviceId int NOT NULL,
                    AccessAllowed bit NOT NULL CONSTRAINT DF_EmployeeFaceDevices_AccessAllowed DEFAULT (1),
                    FaceId nvarchar(256) NULL,
                    SyncedAtUtc datetime2 NULL,
                    CONSTRAINT FK_EmployeeFaceDevices_Employees
                        FOREIGN KEY (EmployeeId) REFERENCES Employees(Id) ON DELETE CASCADE,
                    CONSTRAINT FK_EmployeeFaceDevices_FaceDevices
                        FOREIGN KEY (FaceDeviceId) REFERENCES FaceDevices(Id) ON DELETE CASCADE,
                    CONSTRAINT UQ_EmployeeFaceDevices_Employee_Device UNIQUE (EmployeeId, FaceDeviceId)
                );
            END
            IF NOT EXISTS (
                SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Employees_FaceDevices_FaceDeviceId')
            AND COL_LENGTH('Employees', 'FaceDeviceId') IS NOT NULL
            AND OBJECT_ID(N'dbo.FaceDevices', N'U') IS NOT NULL
            BEGIN
                ALTER TABLE Employees ADD CONSTRAINT FK_Employees_FaceDevices_FaceDeviceId
                    FOREIGN KEY (FaceDeviceId) REFERENCES FaceDevices(Id) ON DELETE SET NULL;
            END
            """);
        await EnsureDefaultFaceDeviceAsync(db, builder.Configuration);
        var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();
        await seeder.SeedAsync();
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database initialization failed during startup. Check SQL Server connection and permissions.");
    }
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("frontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<LiveTransactionsHub>("/hubs/live-transactions");

app.Run();

static async Task EnsureDefaultFaceDeviceAsync(AppDbContext db, IConfiguration configuration)
{
    if (await db.FaceDevices.AnyAsync())
    {
        return;
    }

    var ip = (configuration["ExternalApis:DeviceIp"]
              ?? configuration["FacereaderMiddleware:FaceDeviceIp"]
              ?? "192.168.1.201").Trim();
    if (string.IsNullOrWhiteSpace(ip))
    {
        ip = "192.168.1.201";
    }

    var fd = new FaceDevice
    {
        Name = "Default gate",
        DepartmentLabel = null,
        DeviceIp = ip,
        IsActive = true,
        SortOrder = 0
    };
    db.FaceDevices.Add(fd);
    await db.SaveChangesAsync();

    await db.Employees.Where(e => e.FaceDeviceId == null)
        .ExecuteUpdateAsync(s => s.SetProperty(e => e.FaceDeviceId, fd.Id));
}
