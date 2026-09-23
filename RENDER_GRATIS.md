# Publicar HillApp gratis en Render

Esta versión mantiene Gmail SMTP para desarrollo local y usa Resend por HTTPS en Render.
Esto es necesario porque el plan Free de Render bloquea tráfico SMTP saliente en los puertos 25, 465 y 587.

## 1. Resend

1. Crea una cuenta gratis en https://resend.com usando preferiblemente `cfabiel31@gmail.com`.
2. Crea una API Key.
3. Copia la API Key; se usará únicamente como variable secreta en Render.
4. Para enviar solo a tu propio correo puedes usar el remitente de prueba `HillApp HCR2 <onboarding@resend.dev>`.

## 2. Render

1. Entra a https://dashboard.render.com.
2. New > Blueprint.
3. Conecta GitHub y selecciona `Fabiel109/HillApp`.
4. Render leerá `render.yaml`.
5. Cuando solicite las variables secretas:
   - `ConnectionStrings__TiDB`: tu cadena completa de TiDB Cloud.
   - `Email__ApiKey`: tu API Key de Resend.
6. Confirma que el plan sea `Free` y despliega.

## 3. Actualizar GitHub con esta versión

Copia estos archivos encima de tu proyecto v3 actual, sin borrar `.git` ni `appsettings.Local.json`.
Luego ejecuta:

```powershell
git add .
git commit -m "Adaptar correo para Render gratis"
git push
```

## Desarrollo local

Tu `appsettings.Local.json` actual puede seguir usando Gmail SMTP. No necesitas borrar la contraseña de aplicación de Gmail de tu máquina local.

## Importante

No subas `appsettings.Local.json`, la API Key de Resend ni la contraseña de TiDB a GitHub.
