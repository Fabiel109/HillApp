# Nota para hosting gratis

Para Render Free, esta versión puede usar **Resend por HTTPS** para los comentarios, manteniendo **Gmail SMTP en local**. Consulta `RENDER_GRATIS.md`.

# HillApp v3 — VS Code + ASP.NET Core + TiDB Cloud

Esta versión conserva el panel de récords, la sesión única y los comentarios por correo, y agrega administración de cuentas y perfil.

## Lo nuevo en v3

- **Perfil para todos los usuarios**: muestra usuario, correo, fecha de creación y último acceso.
- Cambio de nombre de usuario.
- Cambio de contraseña solicitando la contraseña actual.
- Eliminación de la propia cuenta solicitando la contraseña actual.
- Al eliminar una cuenta se borran también sus récords y comentarios.
- El login acepta **nombre de usuario o correo**.
- La app guarda `UltimoAcceso` sin borrarlo al cerrar sesión.
- Las cuentas con **500 días sin actividad** se eliminan automáticamente. La limpieza se ejecuta al arrancar la app y vuelve a comprobar cada 24 horas mientras el servidor esté activo.
- En el registro se informa al usuario de la política de 500 días.
- La cuenta cuyo correo registrado sea `cfabiel31@gmail.com` ve una opción **Gestión** que no aparece para los demás usuarios.
- Gestión permite ver cuentas por fecha de creación o último acceso y eliminar cuentas antiguas.
- Acceder directamente a `/Gestion` sin la cuenta autorizada devuelve 404.
- Diseño responsive para Perfil y Gestión.

## 1. Actualiza tu base actual

En TiDB Cloud > SQL Editor ejecuta completo:

`SQL/ACTUALIZAR_BASE_V3.sql`

Ese script agrega `Usuarios.UltimoAcceso`, inicializa el dato para las cuentas existentes y crea un índice para correo. No elimina tus datos existentes.

## 2. Conserva tu configuración privada

La v3 entregada **no incluye credenciales reales**. Copia tu archivo que ya funciona desde la carpeta v2:

`appsettings.Local.json`

hacia la carpeta v3, al mismo nivel de `HillApp.csproj`.

Ese archivo está ignorado por Git mediante `.gitignore` y no debe subirse a GitHub.

Si prefieres recrearlo, copia `appsettings.Local.example.json` como `appsettings.Local.json` y vuelve a poner tu cadena de TiDB y tu contraseña de aplicación de Gmail.

## 3. Ejecuta

Desde la carpeta de `HillApp.csproj`:

```powershell
dotnet restore
dotnet run
```

## 4. Pruebas recomendadas

1. Entra con una cuenta normal y comprueba que aparece **Perfil** pero no **Gestión**.
2. Cambia el nombre de usuario y vuelve a Récords.
3. Cambia la contraseña usando la contraseña actual.
4. Crea una cuenta de prueba y elimínala desde Perfil; comprueba que desaparece de `Usuarios`, `Records` y `Comentarios`.
5. Entra con la cuenta registrada con `cfabiel31@gmail.com`; debe aparecer **Gestión**.
6. Desde Gestión ordena por creación y por último acceso.
7. Elimina una cuenta de prueba desde Gestión y confirma que sus datos asociados desaparecen.

## 5. Hosting

La configuración pública sigue usando variables de entorno:

- `ConnectionStrings__TiDB`
- `Email__Username`
- `Email__Password`
- `Email__FromEmail`
- `Email__ToEmail`

`Admin:OwnerEmail` y `Accounts:InactiveDays` ya tienen valores seguros en `appsettings.json`. Si algún día quieres cambiarlos en el hosting puedes usar:

- `Admin__OwnerEmail`
- `Accounts__InactiveDays`

Nunca publiques `appsettings.Local.json`.
