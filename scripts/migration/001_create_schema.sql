-- =============================================
-- MexClub Database Schema - PostgreSQL
-- Ejecutar con: psql -U mexclub -d MexClubDb -f 001_create_schema.sql
-- =============================================

-- STEP 1: Create tables

CREATE TABLE IF NOT EXISTS "Usuarios" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Nombre" VARCHAR(200) NOT NULL,
    "Apellidos" VARCHAR(200) NOT NULL,
    "TipoDocumento" VARCHAR(20) NOT NULL,
    "Documento" VARCHAR(50) NOT NULL UNIQUE,
    "Pais" VARCHAR(100),
    "Provincia" VARCHAR(100),
    "Localidad" VARCHAR(100),
    "Direccion" VARCHAR(300),
    "CodigoPostal" VARCHAR(10),
    "Telefono1" VARCHAR(20),
    "Telefono2" VARCHAR(20),
    "Email" VARCHAR(200),
    "FechaAlta" TIMESTAMP NOT NULL DEFAULT NOW(),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "Logins" (
    "Id" BIGSERIAL PRIMARY KEY,
    "UsuarioId" BIGINT NOT NULL,
    "NombreUsuario" VARCHAR(100) NOT NULL UNIQUE,
    "PasswordHash" VARCHAR(200) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "Socios" (
    "Id" BIGSERIAL PRIMARY KEY,
    "NumSocio" INT NOT NULL UNIQUE,
    "Codigo" VARCHAR(50) NOT NULL,
    "ReferidoPorSocioId" BIGINT,
    "Nombre" VARCHAR(200) NOT NULL,
    "PrimerApellido" VARCHAR(200) NOT NULL,
    "SegundoApellido" VARCHAR(200),
    "TipoDocumento" VARCHAR(20) NOT NULL,
    "Documento" VARCHAR(50) NOT NULL UNIQUE,
    "Pais" VARCHAR(100),
    "Provincia" VARCHAR(100),
    "Localidad" VARCHAR(100),
    "Direccion" VARCHAR(300),
    "CodigoPostal" VARCHAR(10),
    "Telefono" VARCHAR(20),
    "Email" VARCHAR(200),
    "FechaNacimiento" DATE,
    "FechaAlta" TIMESTAMP NOT NULL DEFAULT NOW(),
    "FotoUrl" VARCHAR(500),
    "FotoAnversoDniUrl" VARCHAR(500),
    "FotoReversoDniUrl" VARCHAR(500),
    "Estrellas" SMALLINT NOT NULL DEFAULT 0,
    "ConsumicionMaximaMensual" INT NOT NULL DEFAULT 0,
    "EsTerapeutica" BOOLEAN NOT NULL DEFAULT FALSE,
    "EsExento" BOOLEAN NOT NULL DEFAULT FALSE,
    "PagoConTarjeta" BOOLEAN NOT NULL DEFAULT FALSE,
    "Comentario" TEXT,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("ReferidoPorSocioId") REFERENCES "Socios"("Id")
);

CREATE TABLE IF NOT EXISTS "SocioDetalles" (
    "SocioId" BIGINT PRIMARY KEY,
    "CuotaFechaProxima" TIMESTAMP,
    "ConsumicionDelMes" NUMERIC(10,2) NOT NULL DEFAULT 0,
    "AportacionDelDia" NUMERIC(10,2),
    "FechaUltimaConsumicion" TIMESTAMP,
    "FechaUltimaAportacion" TIMESTAMP,
    "ExentoCuota" BOOLEAN NOT NULL DEFAULT FALSE,
    "DebeCuota" BOOLEAN NOT NULL DEFAULT FALSE,
    "Aprovechable" NUMERIC(10,2) NOT NULL DEFAULT 0,
    FOREIGN KEY ("SocioId") REFERENCES "Socios"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "UserLogins" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Username" VARCHAR(100) NOT NULL UNIQUE,
    "PasswordHash" VARCHAR(200) NOT NULL,
    "Rol" VARCHAR(20) NOT NULL DEFAULT 'socio',
    "FechaAlta" TIMESTAMP NOT NULL DEFAULT NOW(),
    "SocioId" BIGINT NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("SocioId") REFERENCES "Socios"("Id") ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS "Familias" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Nombre" VARCHAR(200) NOT NULL,
    "Descuento" INT,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP
);

CREATE TABLE IF NOT EXISTS "Articulos" (
    "Id" BIGSERIAL PRIMARY KEY,
    "FamiliaId" BIGINT NOT NULL,
    "Nombre" VARCHAR(200) NOT NULL,
    "Descripcion" TEXT,
    "Precio" NUMERIC(10,2) NOT NULL,
    "Cantidad1" NUMERIC(10,2) NOT NULL,
    "Cantidad2" NUMERIC(10,2),
    "Cantidad3" NUMERIC(10,2),
    "Cantidad4" NUMERIC(10,2),
    "EsDecimal" BOOLEAN NOT NULL DEFAULT FALSE,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("FamiliaId") REFERENCES "Familias"("Id")
);

CREATE TABLE IF NOT EXISTS "Aportaciones" (
    "Id" BIGSERIAL PRIMARY KEY,
    "SocioId" BIGINT NOT NULL,
    "UsuarioId" BIGINT NOT NULL,
    "CantidadAportada" NUMERIC(10,2) NOT NULL,
    "Fecha" TIMESTAMP NOT NULL DEFAULT NOW(),
    "Codigo" VARCHAR(50) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("SocioId") REFERENCES "Socios"("Id"),
    FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios"("Id")
);

CREATE TABLE IF NOT EXISTS "Retiradas" (
    "Id" BIGSERIAL PRIMARY KEY,
    "SocioId" BIGINT NOT NULL,
    "ArticuloId" BIGINT NOT NULL,
    "UsuarioId" BIGINT NOT NULL,
    "PrecioArticulo" NUMERIC(10,2) NOT NULL,
    "Cantidad" NUMERIC(10,2) NOT NULL,
    "Total" NUMERIC(10,2) NOT NULL,
    "FirmaUrl" VARCHAR(500),
    "Fecha" TIMESTAMP NOT NULL DEFAULT NOW(),
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("SocioId") REFERENCES "Socios"("Id"),
    FOREIGN KEY ("ArticuloId") REFERENCES "Articulos"("Id"),
    FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios"("Id")
);

CREATE TABLE IF NOT EXISTS "Cuotas" (
    "Id" BIGSERIAL PRIMARY KEY,
    "SocioId" BIGINT NOT NULL,
    "Fecha" TIMESTAMP NOT NULL DEFAULT NOW(),
    "CantidadCuota" INT NOT NULL,
    "Periodo" INT NOT NULL,
    "UsuarioId" BIGINT NOT NULL,
    "FechaAnterior" TIMESTAMP NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("SocioId") REFERENCES "Socios"("Id"),
    FOREIGN KEY ("UsuarioId") REFERENCES "Usuarios"("Id")
);

CREATE TABLE IF NOT EXISTS "Accesos" (
    "Id" BIGSERIAL PRIMARY KEY,
    "SocioId" BIGINT NOT NULL,
    "TipoAcceso" VARCHAR(50) NOT NULL,
    "FechaHora" TIMESTAMP NOT NULL DEFAULT NOW(),
    "Turno" VARCHAR(50),
    "Accion" VARCHAR(50) NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("SocioId") REFERENCES "Socios"("Id")
);

CREATE TABLE IF NOT EXISTS "RefreshTokens" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Token" VARCHAR(200) NOT NULL UNIQUE,
    "Expiration" TIMESTAMP NOT NULL,
    "IsRevoked" BOOLEAN NOT NULL DEFAULT FALSE,
    "UserLoginId" BIGINT,
    "LoginId" BIGINT,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP,
    FOREIGN KEY ("UserLoginId") REFERENCES "UserLogins"("Id"),
    FOREIGN KEY ("LoginId") REFERENCES "Logins"("Id")
);

CREATE TABLE IF NOT EXISTS "RegistrosBorrados" (
    "Id" BIGSERIAL PRIMARY KEY,
    "Fecha" TIMESTAMP NOT NULL DEFAULT NOW(),
    "Tipo" VARCHAR(100) NOT NULL,
    "Descripcion" TEXT NOT NULL,
    "SocioId" BIGINT,
    "FamiliaId" BIGINT,
    "UsuarioId" BIGINT,
    "ArticuloId" BIGINT,
    "CuotaId" BIGINT,
    "Cantidad" NUMERIC(10,2),
    "Total" NUMERIC(10,2),
    "RealizadoPorUsuarioId" BIGINT NOT NULL,
    "IsActive" BOOLEAN NOT NULL DEFAULT TRUE,
    "CreatedAt" TIMESTAMP NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMP
);

-- STEP 2: Create indexes

CREATE INDEX IF NOT EXISTS "IX_Socios_Codigo" ON "Socios"("Codigo");
CREATE INDEX IF NOT EXISTS "IX_Socios_IsActive" ON "Socios"("IsActive");
CREATE INDEX IF NOT EXISTS "IX_Socios_FechaAlta" ON "Socios"("FechaAlta" DESC);

CREATE INDEX IF NOT EXISTS "IX_Accesos_SocioId" ON "Accesos"("SocioId");
CREATE INDEX IF NOT EXISTS "IX_Accesos_FechaHora" ON "Accesos"("FechaHora" DESC);

CREATE INDEX IF NOT EXISTS "IX_Aportaciones_SocioId" ON "Aportaciones"("SocioId");
CREATE INDEX IF NOT EXISTS "IX_Aportaciones_Fecha" ON "Aportaciones"("Fecha" DESC);

CREATE INDEX IF NOT EXISTS "IX_Retiradas_SocioId" ON "Retiradas"("SocioId");
CREATE INDEX IF NOT EXISTS "IX_Retiradas_Fecha" ON "Retiradas"("Fecha" DESC);

CREATE INDEX IF NOT EXISTS "IX_Cuotas_SocioId" ON "Cuotas"("SocioId");
CREATE INDEX IF NOT EXISTS "IX_Cuotas_Fecha" ON "Cuotas"("Fecha" DESC);

CREATE INDEX IF NOT EXISTS "IX_Articulos_Nombre" ON "Articulos"("Nombre");
CREATE INDEX IF NOT EXISTS "IX_Articulos_IsActive" ON "Articulos"("IsActive");
CREATE INDEX IF NOT EXISTS "IX_Articulos_FamiliaId" ON "Articulos"("FamiliaId");

CREATE INDEX IF NOT EXISTS "IX_Familias_Nombre" ON "Familias"("Nombre");

CREATE INDEX IF NOT EXISTS "IX_RefreshTokens_Token" ON "RefreshTokens"("Token");

-- STEP 3: Seed admin user
-- Password: "admin" (SHA-256 Base64) - CAMBIAR INMEDIATAMENTE
INSERT INTO "Usuarios" ("Nombre", "Apellidos", "TipoDocumento", "Documento", "FechaAlta", "IsActive", "CreatedAt")
SELECT 'Admin', 'Sistema', 'DNI', '00000000A', NOW(), TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM "Usuarios" WHERE "Documento" = '00000000A');

INSERT INTO "Logins" ("UsuarioId", "NombreUsuario", "PasswordHash", "IsActive", "CreatedAt")
SELECT 1, 'admin', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM "Logins" WHERE "NombreUsuario" = 'admin');
