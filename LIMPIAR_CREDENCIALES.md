# 🚨 URGENTE: Limpiar Credenciales de GitHub

## ⚠️ Problema:
GitGuardian detectó credenciales expuestas en tu repositorio público.

## ✅ Solución Paso a Paso:

### 1. **Revocar API Key de Resend:**
- Ir a: https://resend.com/api-keys
- Eliminar la API Key actual
- Crear una nueva
- Actualizar `appsettings.json` local

### 2. **Verificar qué archivos están en GitHub:**
```bash
git ls-files
```

### 3. **Asegurar que appsettings.json está en .gitignore:**
```bash
# Verificar
cat .gitignore | grep appsettings

# Si no está, agregarlo
echo "appsettings.json" >> .gitignore
echo "appsettings.*.json" >> .gitignore
```

### 4. **Eliminar appsettings.json del repositorio (sin borrar local):**
```bash
git rm --cached appsettings.json
git commit -m "Remove sensitive configuration file"
```

### 5. **Limpiar historial de Git (IMPORTANTE):**

**Opción A - Con git filter-repo (recomendado):**
```bash
# Instalar git-filter-repo
pip install git-filter-repo

# Limpiar archivo del historial
git filter-repo --path appsettings.json --invert-paths --force
```

**Opción B - Manualmente:**
```bash
# Eliminar del historial
git filter-branch --force --index-filter \
  "git rm --cached --ignore-unmatch appsettings.json" \
  --prune-empty --tag-name-filter cat -- --all

# Limpiar referencias
rm -rf .git/refs/original/
git reflog expire --expire=now --all
git gc --prune=now --aggressive
```

### 6. **Forzar push al repositorio:**
```bash
git push origin --force --all
git push origin --force --tags
```

### 7. **Cambiar TODAS las credenciales expuestas:**
- ✅ API Key de Resend (nueva)
- ✅ Contraseña de MySQL (`Ag0sM1c4` - cambiarla)
- ✅ SecretKey de JWT (cambiarla)

### 8. **Crear appsettings.Example.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  },
  "AllowedHosts": "*",
  "Salt": "SALADA",
  "TokenAuthentication": {
    "SecretKey": "TU_CLAVE_SECRETA_AQUI",
    "Issuer": "GestionCompras",
    "Audience": "mobileAPP"
  },
  "Resend": {
    "ApiKey": "re_TU_API_KEY_AQUI",
    "FromEmail": "onboarding@resend.dev",
    "FromName": "Sistema Gestión Pañol"
  }
}
```

## 🔒 Prevención Futura:

1. **NUNCA** subir `appsettings.json` a Git
2. Usar variables de entorno en producción
3. Usar `.gitignore` correctamente
4. Revisar antes de hacer commit: `git diff --cached`

## ⚠️ Riesgos si NO lo arreglas:

- ❌ Alguien puede usar tu API Key de Resend (gastar tu cuota)
- ❌ Acceso a tu base de datos MySQL
- ❌ Robo de datos sensibles
- ❌ Resend puede bloquear tu cuenta por seguridad

## 📞 Soporte:

Si necesitas ayuda, contacta a:
- Resend Support: https://resend.com/support
- GitHub Support: https://support.github.com/
