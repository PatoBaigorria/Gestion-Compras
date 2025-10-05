# 🔧 Configuración del Proyecto

## 📋 Pasos para configurar después de clonar el repositorio:

### 1️⃣ **Crear appsettings.json:**

Copia `appsettings.Example.json` y renómbralo a `appsettings.json`:

```bash
copy appsettings.Example.json appsettings.json
```

### 2️⃣ **Configurar credenciales:**

Edita `appsettings.json` y completa:

```json
{
  "TokenAuthentication": {
    "SecretKey": "TU_CLAVE_SECRETA_AQUI"
  },
  "Resend": {
    "ApiKey": "re_TU_API_KEY_DE_RESEND_AQUI"
  }
}
```

### 3️⃣ **Obtener API Key de Resend:**

1. Ir a: https://resend.com/
2. Crear cuenta (gratis - 3,000 emails/mes)
3. Ir a API Keys
4. Crear nueva API Key
5. Copiar y pegar en `appsettings.json`

### 4️⃣ **Configurar Base de Datos:**

En `Program.cs`, actualizar la cadena de conexión:

```csharp
"Server=TU_SERVIDOR;User=TU_USUARIO;Password=TU_PASSWORD;Database=GestionComprasP;..."
```

### 5️⃣ **Compilar para producción:**

```bash
dotnet publish -c Release -r win-x64 --self-contained true
```

Esto generará la carpeta `publish/` con todo lo necesario para el servidor.

### 6️⃣ **Copiar al servidor:**

Copia manualmente la carpeta `publish/` al servidor (USB, red, etc.)

---

## ⚠️ **IMPORTANTE:**

- ❌ **NUNCA** subas `appsettings.json` a GitHub
- ❌ **NUNCA** subas la carpeta `publish/` a GitHub
- ✅ Usa `appsettings.Example.json` como referencia
- ✅ Genera `publish/` localmente cuando lo necesites

---

## 🔒 **Seguridad:**

- Cambia `SecretKey` por una clave única y segura
- Guarda las credenciales en un lugar seguro
- No compartas las API Keys
