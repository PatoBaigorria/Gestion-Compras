using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.Models;
using Microsoft.OpenApi.Models;
using Microsoft.AspNetCore.Authorization;
using Gestion_Compras.Filters;
using Newtonsoft.Json.Converters;
using System.Diagnostics;
using QuestPDF.Infrastructure;
using Gestion_Compras.Services;

var builder = WebApplication.CreateBuilder(args);

// Configurar licencia QuestPDF para uso Community (gratuito)
QuestPDF.Settings.License = LicenseType.Community;

/*Código para hashear la contraseña 
string password = "123"; 
string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password); 
Console.WriteLine($"Contraseña hasheada: {hashedPassword}");*/
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
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Cambiado a None para permitir HTTP
        options.Cookie.SameSite = SameSiteMode.Lax; // Cambiado a Lax para permitir acceso remoto
        options.Cookie.IsEssential = true;

        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        };
    });

var serverVersion = ServerVersion.AutoDetect("Server=localhost;User=root;Password=;Database=GestionComprasP;SslMode=none");

builder.Services.AddDbContext<DataContext>(dbContextOptions => dbContextOptions
    .UseMySql("Server=100.82.200.28;User=mbaigorria;Password=Ag0sM1c4;Database=GestionComprasP;SslMode=none;AllowZeroDateTime=True;ConvertZeroDateTime=True", serverVersion)
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging(false)
    .EnableDetailedErrors()
);

// Registrar servicio de email
builder.Services.AddScoped<IEmailService, EmailService>();

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

// app.UseHttpsRedirection(); // Comentado para permitir acceso HTTP remoto
app.UseStaticFiles();
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseRouting();

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