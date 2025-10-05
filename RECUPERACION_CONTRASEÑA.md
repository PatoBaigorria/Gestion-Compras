# 🔐 Sistema de Recuperación de Contraseña

## ✅ Implementación Completa

Sistema de recuperación de contraseña con **tokens JWT** y envío de emails mediante **Resend**.

---

## 📦 Componentes

### 1. **Modelo Usuario**
- Campo `Email` (requerido, validado)
- Campo `PrimeraVezLogin` para forzar cambio de contraseña

### 2. **Servicio de Email (Resend)**
- `EmailService.cs` - Envío de emails con API REST de Resend
- `IEmailService.cs` - Interfaz del servicio
- **3,000 emails/mes GRATIS para siempre**

### 3. **Controlador de Autenticación**
- `RecuperarPassword` (GET/POST) - Solicitar recuperación
- `RestablecerPassword` (GET/POST) - Restablecer con token
- Tokens JWT con expiración de 5 minutos

### 4. **Vistas**
- `RecuperarPassword.cshtml` - Formulario para ingresar email
- `RestablecerPassword.cshtml` - Formulario para nueva contraseña
- `Login.cshtml` - Con enlace "¿Olvidaste tu contraseña?"

---

## 🔄 Flujo Completo

1. **Usuario olvida contraseña** → Click en "¿Olvidaste tu contraseña?"
2. **Ingresa email** → Sistema valida que exista
3. **Sistema genera token JWT** → Válido por 5 minutos
4. **Envía email con enlace** → Email real a la casilla del usuario
5. **Usuario hace click** → Sistema valida token
6. **Ingresa nueva contraseña** → Se actualiza en BD
7. **Inicia sesión** → Con la nueva contraseña

---

## ⚙️ Configuración

### **appsettings.json:**

```json
{
  "TokenAuthentication": {
    "SecretKey": "Super_Secreta_es_la_clave_de_esta_APP_shhh",
    "Issuer": "GestionCompras",
    "Audience": "mobileAPP"
  },
  "Resend": {
    "ApiKey": "re_TU_API_KEY",
    "FromEmail": "onboarding@resend.dev",
    "FromName": "Sistema Gestión Pañol"
  }
}
```

### **Base de Datos:**

```sql
ALTER TABLE Usuario ADD COLUMN Email VARCHAR(255) NOT NULL AFTER Apellido;
UPDATE Usuario SET Email = 'usuario@email.com' WHERE Id = 1;
```

---

## 🔒 Seguridad

- ✅ Tokens JWT firmados criptográficamente
- ✅ Expiración de 5 minutos
- ✅ Contraseñas hasheadas con BCrypt
- ✅ No revela si un email existe
- ✅ Validación de contraseñas coincidentes
- ✅ Un solo uso por token

---

## 📧 Resend

**Servicio de email utilizado:**
- **Plan Gratuito:** 3,000 emails/mes (para siempre)
- **API Key:** Configurada en appsettings.json
- **Dominio:** onboarding@resend.dev (compartido, ya verificado)
- **Dashboard:** https://resend.com/

---

## 🧪 Pruebas

1. Ir a: http://localhost:5000/Autenticacion/Login
2. Click en "¿Olvidaste tu contraseña?"
3. Ingresar email de usuario
4. Revisar casilla de email
5. Click en el enlace del email
6. Ingresar nueva contraseña
7. Iniciar sesión

---

## 📁 Archivos del Sistema

### **Servicios:**
- `Services/EmailService.cs`
- `Services/IEmailService.cs`

### **Controladores:**
- `Controllers/AutenticacionController.cs`

### **Vistas:**
- `Views/Autenticacion/RecuperarPassword.cshtml`
- `Views/Autenticacion/RestablecerPassword.cshtml`
- `Views/Autenticacion/Login.cshtml`

### **Modelos:**
- `Models/Usuario.cs`

---

## ✅ Sistema Listo para Producción

El sistema está completamente funcional y listo para usar en producción con Resend.
