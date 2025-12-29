using Chemical_Neon_Vending.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSingleton<FileErrorLoggerService>();

// Add IMemoryCache for SessionService
builder.Services.AddMemoryCache();
builder.Services.AddScoped<SessionService>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        builder => builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});

var app = builder.Build();
app.UseCors("AllowAll");
app.MapControllers();
app.Run();
