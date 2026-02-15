-- Check admin logins
SELECT "Id", "NombreUsuario", "PasswordHash", "IsActive" FROM "Logins";

-- Check user logins  
SELECT "Id", "Username", "PasswordHash", "Rol", "IsActive" FROM "UserLogins" LIMIT 10;

-- Reset admin password to 'admin' (SHA-256 Base64)
-- Uncomment if needed:
-- UPDATE "Logins" SET "PasswordHash" = 'jGl25bVBBBW96Qi9Te4V37Fnqchz/Eu4qB9vKrRIqRg=' WHERE "NombreUsuario" = 'admin';
