using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.Models;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using Gestion_Compras.Filters;
using Newtonsoft.Json.Converters;
using System.Diagnostics;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

/*Código para hashear la contraseña 
string password = "123"; 
string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password); 
Console.WriteLine($"Contraseña hasheada: {hashedPassword}");*/

// Configuración de licencia QuestPDF (Community)
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.Converters.Add(new StringEnumConverter());
    });

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new UserNameFilter());
});

// Configuración de autenticación con cookies de sesión
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Autenticacion/Login";
        options.LogoutPath = "/Autenticacion/Logout";
        options.AccessDeniedPath = "/Autenticacion/AccessDenied";

        // Configuración de la cookie
        options.Cookie.Name = ".AspNetCore.Cookies";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.IsEssential = true;

        // ❌ Importante: sin ExpireTimeSpan → será cookie de sesión pura
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // seguridad por inactividad
        options.SlidingExpiration = false;
        options.Cookie.Expiration = null; // al cerrar navegador/pestañas se borra

        // Validar manualmente expiración
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = context =>
            {
                if (context.Properties?.IssuedUtc != null &&
                    DateTimeOffset.UtcNow.Subtract(context.Properties.IssuedUtc.Value) > TimeSpan.FromMinutes(30))
                {
                    context.RejectPrincipal();
                    context.HttpContext.Response.Redirect("/Autenticacion/Login");
                }
                return Task.CompletedTask;
            }
        };

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

var serverVersion = ServerVersion.AutoDetect("Server=localhost;User=root;Password=;Database=GestionComprasP;SslMode=none");

builder.Services.AddDbContext<DataContext>(dbContextOptions => dbContextOptions
    .UseMySql("Server=100.93.151.125;User=mbaigorria;Password=Ag0sM1c4;Database=GestionComprasP;SslMode=none;AllowZeroDateTime=True;ConvertZeroDateTime=True", serverVersion)
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging(false)
    .EnableDetailedErrors()
);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Gestion Compras API", Version = "v1" });
});

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Administrador", policy => policy.RequireRole("Administrador"));
    options.AddPolicy("Pañolero", policy => policy.RequireRole("Pañolero"));
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseRouting();

// Middleware para detectar si es nueva sesión de navegador
app.Use(async (context, next) =>
{
    if (!context.Request.Cookies.ContainsKey("BrowserSessionStarted"))
    {
        // Si no existe, eliminamos cualquier cookie de auth previa
        context.Response.Cookies.Delete(".AspNetCore.Cookies");
        context.Response.Cookies.Append("BrowserSessionStarted", "true");
    }
    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// Rutas
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapControllers();

// Configuración para LAN
app.Urls.Clear();
app.Urls.Add("http://localhost:5000");
app.Urls.Add("http://*:5000");

// Abrir navegador automáticamente
var url = "http://localhost:5000";
Task.Run(() =>
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error al abrir el navegador: {ex.Message}");
    }
});

app.Run();