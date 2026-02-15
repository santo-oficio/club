-- Ensure admin usuario exists
INSERT INTO "Usuarios" ("Nombre", "Apellidos", "TipoDocumento", "Documento", "FechaAlta", "IsActive", "CreatedAt")
SELECT 'Admin', 'Sistema', 'DNI', '00000000A', NOW(), TRUE, NOW()
WHERE NOT EXISTS (SELECT 1 FROM "Usuarios" WHERE "Documento" = '00000000A');

-- Insert admin login: username=admin, password=admin (SHA-256 Base64)
INSERT INTO "Logins" ("UsuarioId", "NombreUsuario", "PasswordHash", "IsActive", "CreatedAt")
SELECT u."Id", 'admin', 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=', TRUE, NOW()
FROM "Usuarios" u WHERE u."Documento" = '00000000A'
AND NOT EXISTS (SELECT 1 FROM "Logins" WHERE "NombreUsuario" = 'admin');

-- Verify
SELECT "Id", "NombreUsuario", "IsActive" FROM "Logins";
