using Chemical_Neon.Services;
using System;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddSingleton<FileErrorLoggerService>();



builder.Services.AddEndpointsApiExplorer();

// Add CSRF/AntiForgery protection
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false; // Allow JavaScript to read for header transmission
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});

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

app.UseAntiforgery();
app.UseCors("AllowAll");

app.MapControllers();

app.Run();
