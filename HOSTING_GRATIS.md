# Publicar HillApp gratis

## Lo importante primero

No existe un hosting dinámico gratuito que pueda prometer 100% de disponibilidad permanente. Los planes gratis suelen dormir, tener cuotas o poder cambiar sus condiciones.

Para esta app, dos opciones sencillas son:

- **Koyeb Free**: una instancia gratis; actualmente escala a cero después de 1 hora sin tráfico. Cuando alguien vuelve a entrar, se reactiva.
- **Render Free**: muy fácil de usar, pero actualmente duerme después de 15 minutos sin tráfico.

La aplicación ya está preparada para esos reinicios: la sesión válida se guarda mediante token en TiDB, no solamente en memoria del servidor.

Si más adelante necesitas disponibilidad real 24/7 sin cold starts, lo correcto es pasar a un plan de pago pequeño. Oracle Cloud tiene recursos Always Free, pero puede reclamar instancias consideradas inactivas y su configuración es bastante más técnica.

## Opción recomendada para empezar: Koyeb

### A. Subir el proyecto a GitHub

Antes de subirlo:

1. Confirma que `appsettings.Local.json` NO aparece para subir. Está en `.gitignore`.
2. No escribas la contraseña de TiDB ni la contraseña de aplicación de Gmail en archivos públicos.
3. Sube el proyecto con `Dockerfile` incluido.

### B. Crear el Web Service

1. Crea una cuenta en Koyeb.
2. Elige **Create Web Service**.
3. Elige GitHub y conecta el repositorio.
4. Selecciona el builder **Dockerfile / Docker**.
5. Para la instancia elige **Free**.
6. Si te deja escoger región gratuita, Washington D.C. suele ser una opción razonable para usuarios de América.
7. Configura puerto HTTP `8080` y ruta `/` si te lo pide.
8. Agrega estas variables de entorno:

```text
PORT=8080
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__TiDB=TU_CADENA_COMPLETA_REAL_DE_TIDB
Email__Host=smtp.gmail.com
Email__Port=587
Email__Username=cfabiel31@gmail.com
Email__Password=TU_PASSWORD_DE_APLICACION_GMAIL
Email__FromEmail=cfabiel31@gmail.com
Email__ToEmail=cfabiel31@gmail.com
```

9. Despliega.
10. Koyeb te dará una URL pública terminada en `.koyeb.app`.
11. Abre `/healthz`; debe responder un estado `ok`.
12. Prueba registro, login, récords y comentarios.

## Alternativa: Render

El archivo `render.yaml` ya está incluido.

1. Sube el proyecto a GitHub.
2. En Render crea un Blueprint/Web Service usando ese repositorio.
3. Render detectará `render.yaml` y `Dockerfile`.
4. Te pedirá los secretos marcados con `sync: false`:
   - `ConnectionStrings__TiDB`
   - `Email__Username`
   - `Email__Password`
5. Despliega.

Recuerda que el plan Free de Render puede dormir el servicio después de 15 minutos sin tráfico, por lo que la primera visita después de un rato puede tardar más.

## Base de datos

La base sigue estando en **TiDB Cloud**. El hosting solamente ejecuta la aplicación ASP.NET Core.

No uses una base local dentro del contenedor, porque el almacenamiento del plan gratuito puede ser efímero.
