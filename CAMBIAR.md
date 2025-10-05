# 🔐 Para que funcione de Servidor mi pc local

## ✅ Implementación Completa


var serverVersion = ServerVersion.AutoDetect("Server=localhost;User=root;Password=;Database=GestionComprasP;SslMode=none");

# Cambiar y colocar en Server la IP de Tailscale de mi pc
builder.Services.AddDbContext<DataContext>(dbContextOptions => dbContextOptions
    .UseMySql("Server=100.93.151.125;User=mbaigorria;Password=Ag0sM1c4;Database=GestionComprasP;SslMode=none;AllowZeroDateTime=True;ConvertZeroDateTime=True", serverVersion)
    .LogTo(Console.WriteLine, LogLevel.Information)
    .EnableSensitiveDataLogging(false)
    .EnableDetailedErrors()
);