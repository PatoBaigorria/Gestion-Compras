using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Gestion_Compras.Models;
using Microsoft.OpenApi.Models;
using Gestion_Compras.Filters;
using Newtonsoft.Json.Converters;
using System.Diagnostics;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

/*Código para hashear la contraseña 
string password = "123"; 
string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password); 
Console.WriteLine($"Contraseña hasheada: {hashedPassword}");*/


// Identity removido - usando autenticación personalizada con cookies

builder.Logging.AddDebug();

// Configuración de licencia QuestPDF (Community)
QuestPDF.Settings.License = LicenseType.Community;

// Configurar servicios
builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.Converters.Add(new StringEnumConverter());
    });

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new UserNameFilter()); // Registrar el filtro de acción globalmente
});

// Configuración removida - se usa app.Urls.Add más abajo

// Configuración de autenticación con cookies
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Autenticacion/Login";
        options.LogoutPath = "/Autenticacion/Logout";
        options.AccessDeniedPath = "/Autenticacion/AccessDenied";
    });

var serverVersion = ServerVersion.AutoDetect("Server=localhost;User=root;Password=;Database=GestionComprasP;SslMode=none");

builder.Services.AddDbContext<DataContext>(dbContextOptions => dbContextOptions
    .UseMySql("Server=localhost;User=root;Password=;Database=GestionComprasP;SslMode=none", serverVersion)
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
});

// Configuración de logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Information);

var app = builder.Build();



app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors(x => x.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.MapControllerRoute(
    name: "proveedor",
    pattern: "Proveedor/{action=Index}/{id?}",
    defaults: new { controller = "Proveedor", action = "Index" }
);

app.MapControllerRoute(
    name: "familiaSubfamilia",
    pattern: "FamiliaSubfamilia/{action=Index}/{id?}",
    defaults: new { controller = "FamiliaSubfamilia", action = "Index" }
);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.MapControllerRoute(
    name: "salida",
    pattern: "Salida/{action=AltaSalidas}",
    defaults: new { controller = "Salida", action = "AltaSalidas" }
);

app.MapControllerRoute(
    name: "ingreso",
    pattern: "Ingreso/{action=AltaIngresos}",
    defaults: new { controller = "Ingreso", action = "AltaIngresos" }
);

app.MapControllerRoute(
    name: "ingreso",
    pattern: "Ingreso/{action=Index}",
    defaults: new { controller = "Ingreso", action = "Index" }
);

app.MapControllerRoute(
    name: "pedido",
    pattern: "Pedido/{action=Lista}/{id?}",
    defaults: new { controller = "PedidoView", action = "Lista" }
);

app.MapControllerRoute(
    name: "pedidoNuevo",
    pattern: "Pedido/Nuevo",
    defaults: new { controller = "PedidoView", action = "Index" }
);

app.MapControllers(); // Mapea los controladores restantes

// Configuración para LAN - escuchar en todas las interfaces
app.Urls.Clear(); // Limpiar URLs existentes
app.Urls.Add("http://localhost:5000");
app.Urls.Add("http://*:5000");

// Abrir la aplicación en el navegador con localhost
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

// Ejecutar la aplicación y cerrar la consola
app.Run();
