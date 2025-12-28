using Chemical_Neon.Services;
using System;

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

// Register IHttpContextAccessor so controllers can request it
//builder.Services.AddHttpContextAccessor();

// Add session support since the controller reads HttpContext.Session
//builder.Services.AddDistributedMemoryCache();
//builder.Services.AddSession(options =>
//{
//    options.IdleTimeout = TimeSpan.FromMinutes(2);
//    options.Cookie.HttpOnly = true;
//    options.Cookie.IsEssential = true;
//});

var app = builder.Build();

// Configure the HTTP request pipeline.

//app.UseSession();
//app.UseAuthorization();

app.UseCors("AllowAll");

app.MapControllers();

app.Run();
