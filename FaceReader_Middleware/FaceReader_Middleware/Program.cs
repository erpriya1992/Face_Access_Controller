using FaceReader_Middleware.Models;
using FaceReader_Middleware.Services;
using System.Net;
using System.Net.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.Configure<DeviceSettings>(builder.Configuration.GetSection("DeviceSettings"));
builder.Services.AddHttpClient();

static IHttpClientBuilder AddDeviceFacingHttpClient<TClient>(IServiceCollection services)
    where TClient : class
{
    return services.AddHttpClient<TClient>()
        .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            ConnectTimeout = TimeSpan.FromSeconds(20),
            AutomaticDecompression = DecompressionMethods.All
        })
        .ConfigureHttpClient(c => c.Timeout = TimeSpan.FromSeconds(90));
}

AddDeviceFacingHttpClient<RecognitionRecordService>(builder.Services);
AddDeviceFacingHttpClient<RecognitionRecordDeleteService>(builder.Services);
AddDeviceFacingHttpClient<FindFaceRecognitionRecordService>(builder.Services);
AddDeviceFacingHttpClient<DeleteFaceRecordsService>(builder.Services);
AddDeviceFacingHttpClient<CardRecordService>(builder.Services);
builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
