# Guía de Instalación - MexClub

## Arquitectura de Despliegue

```
┌─────────────────────────────────────────────────────┐
│                    SERVIDOR                          │
│                                                     │
│  ┌──────────────┐    ┌──────────────────────────┐   │
│  │  PostgreSQL   │◄──│  MexClub.Api (.NET 10)   │   │
│  │  (MexClubDb)  │    │                          │   │
│  └──────────────┘    │  • REST API  (/api/...)   │   │
│                      │  • Archivos estáticos     │   │
│                      │    (wwwroot/ = la "app")   │   │
│                      │  • Swagger  (/swagger)    │   │
│                      └─────────┬────────────────┘   │
│                                │ HTTPS              │
└────────────────────────────────┼─────────────────────┘
                                 │
                    ┌────────────┴────────────┐
                    │       INTERNET           │
                    └────────────┬────────────┘
                                 │
              ┌──────────────────┼──────────────────┐
              │                  │                   │
        ┌─────┴─────┐    ┌─────┴─────┐     ┌──────┴──────┐
        │  Tablet    │    │  Móvil     │     │  PC/Mac     │
        │  (PWA)     │    │  (PWA)     │     │  (Navegador)│
        └───────────┘    └───────────┘     └─────────────┘
```

**La API y el cliente son componentes separados pero se sirven desde el mismo servidor.**
La app cliente (HTML/CSS/JS) vive en `wwwroot/` dentro del proyecto API.
Cuando un usuario accede desde un móvil/tablet e "instala" la app, se convierte en una
**PWA (Progressive Web App)** que se ve y se comporta exactamente como una app nativa:
sin barra de navegador, icono en el escritorio, pantalla completa.

---

## PARTE 1: Instalación del Servidor (API + Base de Datos)

### 1.1 Requisitos del Servidor

- **Windows Server 2019+** (o Windows 10/11 para desarrollo)
- **IIS 10+** con el módulo ASP.NET Core Hosting Bundle
- **PostgreSQL 15+** (gratuito, ligero)
- **.NET 10 Runtime** (solo el runtime, no el SDK)
- **Certificado SSL** (obligatorio para que la PWA funcione)

### 1.2 Instalar .NET 10 Hosting Bundle

Descargar desde: `https://dotnet.microsoft.com/download/dotnet/10.0`

Elegir: **Hosting Bundle** (incluye runtime + módulo IIS)

```powershell
# Verificar instalación
dotnet --list-runtimes
```

### 1.3 Instalar y Configurar PostgreSQL

1. Descargar PostgreSQL desde: `https://www.postgresql.org/download/windows/`

2. Durante la instalación, anotar la contraseña del usuario `postgres`.

3. Crear el usuario y la base de datos:

```powershell
# Conectar como superusuario
psql -U postgres -p 4433
```

```sql
-- Crear usuario para la app
CREATE USER mexclub WITH PASSWORD 'TuPasswordSegura';

-- Crear la base de datos
CREATE DATABASE "MexClubDb" OWNER mexclub;

-- Salir
\q
```

4. Ejecutar el script de esquema:

```powershell
psql -U mexclub -d MexClubDb -p 4433 -f scripts\migration\001_create_schema.sql
```

Esto crea todas las tablas, índices, y un usuario admin.

- **Usuario app:** `admin`
- **Password:** `admin`
- **IMPORTANTE:** cambiar la contraseña inmediatamente tras el primer acceso.

### 1.4 Publicar la Aplicación

Desde la máquina de desarrollo:

```powershell
cd "C:\Proyectos personales\Mexclub cliente y servidor\src\MexClub.Api"

# Publicar en modo Release
dotnet publish -c Release -o C:\publish\MexClub
```

Esto genera una carpeta `C:\publish\MexClub\` con todo lo necesario.
Copiar esa carpeta al servidor.

### 1.5 Configurar en IIS

1. **Abrir IIS Manager** (`inetmgr`)

2. **Crear un nuevo sitio web:**
   - Nombre: `MexClub`
   - Ruta física: `C:\inetpub\wwwroot\MexClub` (copiar aquí los archivos publicados)
   - Enlace: `https` puerto `443`
   - Certificado SSL: seleccionar tu certificado

3. **Configurar el Application Pool:**
   - Abrir "Application Pools"
   - Seleccionar el pool de MexClub
   - Cambiar "**.NET CLR Version**" a → **No Managed Code**
   - Modo pipeline: **Integrated**

4. **Verificar web.config** (se genera automáticamente con `dotnet publish`):

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <location path="." inheritInChildApplications="false">
    <system.webServer>
      <handlers>
        <add name="aspNetCore" path="*" verb="*"
             modules="AspNetCoreModuleV2" resourceType="Unspecified" />
      </handlers>
      <aspNetCore processPath="dotnet"
                  arguments=".\MexClub.Api.dll"
                  stdoutLogEnabled="false"
                  stdoutLogFile=".\logs\stdout"
                  hostingModel="InProcess" />
    </system.webServer>
  </location>
</configuration>
```

### 1.6 Configurar Secrets (Producción)

**NUNCA poner secrets en appsettings.json en producción.**

Opción A - Variables de entorno (recomendado en IIS):

En IIS Manager → tu sitio → Configuration Editor → `system.webServer/aspNetCore` → `environmentVariables`:

```
ConnectionStrings__MexClubDb = Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=TuPasswordSegura
Jwt__Key = TU-CLAVE-SECRETA-MINIMO-32-CARACTERES-ALEATORIA
Jwt__Issuer = MexClub.Api
Jwt__Audience = MexClub.Client
```

Opción B - Desde PowerShell (variables de entorno de máquina):

```powershell
[Environment]::SetEnvironmentVariable("ConnectionStrings__MexClubDb", "Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=TuPasswordSegura", "Machine")
[Environment]::SetEnvironmentVariable("Jwt__Key", "clave-super-secreta-de-32-chars-min", "Machine")
```

> **IMPORTANTE:** La connection string en `appsettings.json` tiene password vacío.
> En producción, configura la connection string completa con password vía variables de entorno.

### 1.7 Configurar HTTPS (Obligatorio)

La PWA **requiere HTTPS** para funcionar. Opciones:

- **Let's Encrypt** (gratuito): usar `win-acme` (`https://www.win-acme.com/`)
- **Certificado comercial**: instalar en IIS y enlazar al sitio
- **Desarrollo local**: `dotnet dev-certs https --trust`

### 1.8 Configurar Firewall

```powershell
# Abrir puerto 443 (HTTPS)
New-NetFirewallRule -DisplayName "MexClub HTTPS" -Direction Inbound -Protocol TCP -LocalPort 443 -Action Allow
```

### 1.9 Verificar Instalación del Servidor

Desde un navegador en el servidor:

```
https://localhost/api/ping
```

Debe responder:
```json
{"success": true, "data": "OK"}
```

Swagger disponible en:
```
https://localhost/swagger
```

---

## PARTE 2: Instalación en Tablet / Móvil (La "App")

### Concepto Clave

La aplicación móvil es una **PWA (Progressive Web App)**. Esto significa:

- Se accede inicialmente desde el navegador
- Se "instala" en el dispositivo con un toque
- Una vez instalada: **icono propio, pantalla completa, sin barra de navegador**
- Se ve y se comporta **idéntica a una app nativa**
- No necesita App Store ni Google Play
- Se actualiza automáticamente

### 2.1 Instalación en Android (Tablet o Móvil)

1. Abrir **Google Chrome** en el dispositivo

2. Navegar a la URL del servidor:
   ```
   https://tudominio.com
   ```

3. Iniciar sesión (para verificar que funciona)

4. Chrome mostrará automáticamente un banner: **"Añadir MexClub a pantalla de inicio"**

   Si no aparece el banner automático:
   - Tocar el menú **⋮** (tres puntos) arriba a la derecha
   - Seleccionar **"Instalar aplicación"** o **"Añadir a pantalla de inicio"**

5. Confirmar la instalación

6. **¡Listo!** Aparece el icono "MexClub" en el escritorio del dispositivo.
   Al abrirla se ejecuta a pantalla completa, sin barra de navegador.

### 2.2 Instalación en iPad / iPhone

1. Abrir **Safari** (obligatorio, Chrome en iOS no soporta PWA)

2. Navegar a:
   ```
   https://tudominio.com
   ```

3. Tocar el botón **Compartir** (cuadrado con flecha hacia arriba ↑)

4. En el menú, buscar y tocar **"Añadir a pantalla de inicio"**

5. Editar el nombre si se desea → Tocar **"Añadir"**

6. **¡Listo!** El icono aparece en el escritorio.
   Se abre a pantalla completa, como una app nativa.

### 2.3 Instalación en Windows (PC de escritorio/barra)

1. Abrir **Microsoft Edge** o **Google Chrome**

2. Navegar a `https://tudominio.com`

3. En la barra de direcciones aparece un icono de instalación (⊕)
   O ir a menú → **"Instalar MexClub"**

4. Se instala como aplicación de escritorio con su propia ventana

---

## PARTE 3: Configuración de Red

### 3.1 Escenario Típico: Red Local

Si el servidor está en la misma red local que las tablets:

```
Servidor: 192.168.1.100 (IP fija)
Tablets:  192.168.1.x (DHCP o fija)

URL de acceso: https://192.168.1.100
```

Para esto necesitas un certificado SSL que cubra la IP o un dominio local.

### 3.2 Escenario con Dominio Público

```
Servidor: en hosting o VPS con IP pública
Dominio:  app.tuclub.es (apuntando al servidor)

URL de acceso: https://app.tuclub.es
```

### 3.3 Escenario Mixto (Recomendado)

Usar un servicio de túnel o DNS dinámico para acceso remoto:

- **Cloudflare Tunnel** (gratuito): expone tu servidor local a Internet con HTTPS automático
- **ngrok** (para pruebas)
- **DuckDNS** + Let's Encrypt (gratuito, para IP dinámica)

---

## PARTE 4: Generar los Iconos de la App

La PWA necesita iconos en múltiples tamaños. Estos van en `wwwroot/icons/`.

### 4.1 Crear los iconos

1. Diseña o elige un icono cuadrado de **512x512 px** (PNG, fondo transparente o con fondo)

2. Usa un generador online para crear todos los tamaños:
   - `https://www.pwabuilder.com/imageGenerator`
   - `https://realfavicongenerator.net/`

3. Genera los siguientes tamaños y colócalos en `wwwroot/icons/`:

```
icons/
├── icon-72.png     (72x72)
├── icon-96.png     (96x96)
├── icon-128.png    (128x128)
├── icon-144.png    (144x144)
├── icon-152.png    (152x152)
├── icon-192.png    (192x192)
├── icon-384.png    (384x384)
└── icon-512.png    (512x512)
```

### 4.2 Método rápido con PowerShell y un PNG de 512px

Si tienes **ImageMagick** instalado:

```powershell
$sizes = @(72, 96, 128, 144, 152, 192, 384, 512)
foreach ($s in $sizes) {
    magick icon-512.png -resize "${s}x${s}" "icon-${s}.png"
}
```

---

## PARTE 5: Conexión Cliente → API (Configuración)

### 5.1 Si todo está en el mismo servidor (por defecto)

No hace falta cambiar nada. El archivo `wwwroot/js/config.js` ya viene con:

```javascript
var MEXCLUB_CONFIG = {
    API_BASE_URL: "/api",
    APP_NAME: "MexClub"
};
```

### 5.2 Si la app y la API estuvieran en servidores separados

Editar `wwwroot/js/config.js`:

```javascript
var MEXCLUB_CONFIG = {
    API_BASE_URL: "https://api.tudominio.com/api",
    APP_NAME: "MexClub"
};
```

Y en el servidor API, configurar CORS en `appsettings.json` para permitir el dominio del cliente.

---

## PARTE 6: Actualización de la App

### 6.1 Actualizar el Servidor

```powershell
# En la máquina de desarrollo
cd "C:\Proyectos personales\Mexclub cliente y servidor\src\MexClub.Api"
dotnet publish -c Release -o C:\publish\MexClub

# Copiar al servidor (reemplazar archivos)
# Reiniciar el Application Pool en IIS
```

### 6.2 Actualizar la App en los Dispositivos

**No hay que hacer nada.** La PWA se actualiza sola:

- El **Service Worker** detecta cambios automáticamente
- La próxima vez que el usuario abra la app, descarga la versión nueva
- Los archivos estáticos se cachean para funcionar offline

Para forzar actualización: en `sw.js` cambiar el valor de `CACHE_NAME`:

```javascript
var CACHE_NAME = "mexclub-v2";  // Incrementar versión
```

---

## PARTE 7: Checklist de Verificación

### Servidor

- [ ] .NET 10 Hosting Bundle instalado
- [ ] PostgreSQL instalado y ejecutándose
- [ ] BD `MexClubDb` creada con `001_create_schema.sql` (incluye admin)
- [ ] Datos migrados desde `ClubDB.sdf` (si aplica, ver PARTE 8)
- [ ] Aplicación publicada (`dotnet publish -c Release`)
- [ ] Sitio creado en IIS con HTTPS
- [ ] Application Pool en "No Managed Code"
- [ ] Variables de entorno configuradas (`ConnectionStrings__MexClubDb` + `Jwt__Key`)
- [ ] Certificado SSL válido
- [ ] Firewall con puerto 443 abierto
- [ ] `https://tudominio.com/api/ping` responde OK
- [ ] Swagger accesible en `https://tudominio.com/swagger`

### Dispositivos Móviles

- [ ] Acceso a `https://tudominio.com` desde Chrome (Android) o Safari (iOS)
- [ ] Login funciona correctamente
- [ ] App instalada ("Añadir a pantalla de inicio")
- [ ] Se abre a pantalla completa sin barra de navegador
- [ ] Icono visible en el escritorio del dispositivo
- [ ] Navegación fluida entre secciones
- [ ] Fichaje funciona correctamente

---

## Preguntas Frecuentes

**P: ¿Necesito publicar en Google Play o App Store?**
R: No. La PWA se instala directamente desde el navegador. No necesita tienda de aplicaciones.

**P: ¿Se ve como una app nativa?**
R: Sí. Pantalla completa, icono propio, sin barra de direcciones, splash screen al abrir. Indistinguible de una app nativa para el usuario.

**P: ¿Funciona sin Internet?**
R: La interfaz se carga desde caché (funciona offline). Las operaciones que necesitan datos del servidor requieren conexión.

**P: ¿Cuántas tablets/móviles puedo conectar?**
R: Sin límite. Cada dispositivo accede vía navegador y tiene su propia sesión JWT.

**P: ¿Cómo cambio el nombre o logo de la app?**
R: Editar `wwwroot/manifest.json` (nombre) y reemplazar los archivos en `wwwroot/icons/` (logo). Incrementar la versión del Service Worker.

**P: ¿Puedo usar una IP en vez de dominio?**
R: Sí, pero necesitarás un certificado SSL que cubra esa IP. Para red local es más práctico usar un dominio local con mkcert o similar.

**P: ¿Qué base de datos usa?**
R: PostgreSQL. Es gratuito, ligero y muy robusto. Se instala en minutos y no necesita mantenimiento especial.

---

## PARTE 8: Migración de Datos desde ClubDB.sdf

Si ya tienes datos en el sistema legacy (`ClubDB.sdf`), hay una herramienta que los migra automáticamente a PostgreSQL.

### 8.1 Ubicación

```
scripts/migration/MigradorDatos/
├── MigradorDatos.csproj
└── Program.cs
```

### 8.2 Lo que hace

La herramienta lee cada tabla del `.sdf` antiguo y la inserta en PostgreSQL:

| Tabla .sdf | Tabla PostgreSQL | Notas |
|---|---|---|
| `Usuario` | `Usuarios` | `CP` → `CodigoPostal`, `Fax` eliminado |
| `Login` | `Logins` | **Passwords hasheadas a SHA-256** (estaban en texto plano) |
| `Socio` | `Socios` | `ReferidoPor` (código) → `ReferidoPorSocioId` (FK) resuelto |
| `TablaAuxiliarSocio` | `SocioDetalles` | Mapeo directo |
| `UserLogin` | `UserLogins` | **Passwords hasheadas**, `Usermode` → `Rol` |
| `Familia` | `Familias` | `IdUsuario` eliminado |
| `Articulo` | `Articulos` | `Decimal` → `EsDecimal`, `IdUsuario` eliminado |
| `Aportacion` | `Aportaciones` | Mapeo directo |
| `Retirada` | `Retiradas` | `Firma` → `FirmaUrl` |
| `Cuota` | `Cuotas` | Mapeo directo |
| `Acceso` | `Accesos` | `FechaYHora` → `FechaHora` |
| `Borrado` | `RegistrosBorrados` | `RealizadoPor` → `RealizadoPorUsuarioId` |

### 8.3 Cómo ejecutar la migración

**Paso 1**: Crear primero la BD con el esquema (ver sección 1.3):

```powershell
psql -U mexclub -d MexClubDb -p 4433 -f scripts\migration\001_create_schema.sql
```

**Paso 2**: Compilar el migrador (requiere .NET Framework 4.8.1 SDK):

```powershell
cd scripts\migration\MigradorDatos
dotnet build -c Release
```

**Paso 3**: Ejecutar la migración:

```powershell
# Sintaxis: MigradorDatos.exe <ruta_clubdb.sdf> <connection_string_postgresql>
bin\Release\net481\MigradorDatos.exe "C:\Server\ClubDB.sdf" "Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=TuPassword"
```

La salida será algo como:

```
═══════════════════════════════════════════════
  MexClub - Migrador de Datos
  ClubDB.sdf (SQL CE) → PostgreSQL
═══════════════════════════════════════════════

  ✓ Usuarios                  →     3 registros
  ✓ Logins                    →     3 registros
  ✓ Socios                    →   245 registros
      └─ 12 referencias 'ReferidoPor' resueltas
  ✓ SocioDetalles             →   245 registros
  ✓ UserLogins                →    18 registros
  ✓ Familias                  →     8 registros
  ✓ Articulos                 →    42 registros
  ✓ Aportaciones              →  1830 registros
  ✓ Retiradas                 →  5621 registros
  ✓ Cuotas                    →   890 registros
  ✓ Accesos                   →  3210 registros
  ✓ RegistrosBorrados         →    15 registros

═══ MIGRACIÓN COMPLETADA: 12130 registros totales ═══
```

### 8.4 Notas importantes

- **Las contraseñas se hashean automáticamente** durante la migración. En el sistema viejo estaban en texto plano, ahora se guardan como SHA-256/Base64.
- **Los usuarios deberán usar las mismas contraseñas** que tenían antes, pero ahora estarán almacenadas de forma segura.
- **El campo `ReferidoPor`** en el sistema viejo era un texto (el código del socio referente). El migrador lo resuelve automáticamente a un ID de socio (FK).
- **Las tablas temporales** (`SocioTemporal`, `tablaTemporalSocio`, `ListaCompraTemporal`, `BorradoCuota`) **no se migran** porque son datos volátiles sin valor histórico.
- La migración es **idempotente**: si la ejecutas dos veces, no duplica datos (usa `ON CONFLICT DO NOTHING`).
- Las **secuencias SERIAL** se ajustan automáticamente al final de la migración para que los nuevos registros obtengan IDs correctos.
