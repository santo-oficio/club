using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlServerCe;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Npgsql;

namespace MigradorDatos
{
    /// <summary>
    /// Herramienta de migración de datos: ClubDB.sdf (SQL CE 4.0) → PostgreSQL
    /// 
    /// Uso:
    ///   MigradorDatos.exe "C:\Server\ClubDB.sdf" "Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=pass"
    /// 
    /// Requisitos:
    ///   - La BD PostgreSQL debe existir con el esquema ya creado (001_create_schema.sql)
    ///   - El archivo ClubDB.sdf debe ser accesible
    /// </summary>
    class Program
    {
        static int _totalMigrated = 0;

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine("  MexClub - Migrador de Datos");
            Console.WriteLine("  ClubDB.sdf (SQL CE) → PostgreSQL");
            Console.WriteLine("═══════════════════════════════════════════════");
            Console.WriteLine();

            string sdfPath = args.Length > 0 ? args[0] : @"C:\Server\ClubDB.sdf";
            string pgConn = args.Length > 1 ? args[1] : "Host=localhost;Port=4433;Database=MexClubDb;Username=mexclub;Password=mexclub";

            if (!File.Exists(sdfPath))
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR: No se encuentra el archivo .sdf: {sdfPath}");
                Console.ResetColor();
                Console.WriteLine("Uso: MigradorDatos.exe <ruta_clubdb.sdf> <connection_string_postgresql>");
                return;
            }

            string ceConn = $"Data Source={sdfPath};Max Database Size=4091;";

            // Asegurar UTF-8 en la conexión PostgreSQL
            if (!pgConn.Contains("Client Encoding", StringComparison.OrdinalIgnoreCase))
                pgConn += ";Client Encoding=UTF8";
            if (!pgConn.Contains("Encoding", StringComparison.OrdinalIgnoreCase))
                pgConn += ";Encoding=UTF8";

            try
            {
                using (var ceConnection = new SqlCeConnection(ceConn))
                using (var pgConnection = new NpgsqlConnection(pgConn))
                {
                    ceConnection.Open();
                    pgConnection.Open();

                    // Forzar UTF-8 en la sesión PostgreSQL para evitar conversiones WIN1252
                    ExecutePg(pgConnection, "SET client_encoding TO 'UTF8';");

                    Console.WriteLine($"Origen:  {sdfPath}");
                    Console.WriteLine($"Destino: PostgreSQL ({pgConnection.Host}:{pgConnection.Port}/{pgConnection.Database})");
                    Console.WriteLine();

                    // Desactivar triggers FK durante migración
                    ExecutePg(pgConnection, "SET session_replication_role = 'replica';");

                    // Orden de migración respeta dependencias FK
                    MigrateUsuarios(ceConnection, pgConnection);
                    MigrateLogins(ceConnection, pgConnection);
                    MigrateSocios(ceConnection, pgConnection);
                    MigrateSocioDetalles(ceConnection, pgConnection);
                    MigrateUserLogins(ceConnection, pgConnection);
                    MigrateFamilias(ceConnection, pgConnection);
                    MigrateArticulos(ceConnection, pgConnection);
                    MigrateAportaciones(ceConnection, pgConnection);
                    MigrateRetiradas(ceConnection, pgConnection);
                    MigrateCuotas(ceConnection, pgConnection);
                    MigrateAccesos(ceConnection, pgConnection);
                    MigrateBorrados(ceConnection, pgConnection);

                    // Reactivar triggers FK
                    ExecutePg(pgConnection, "SET session_replication_role = 'origin';");

                    // Ajustar secuencias SERIAL al máximo ID insertado
                    ResetSequences(pgConnection);
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"═══ MIGRACIÓN COMPLETADA: {_totalMigrated} registros totales ═══");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"ERROR FATAL: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                Console.ResetColor();
            }

            Console.WriteLine();
            Console.WriteLine("Pulsa cualquier tecla para salir...");
            Console.ReadKey();
        }

        // ════════════════════════════════════════════
        // USUARIOS
        // ════════════════════════════════════════════
        static void MigrateUsuarios(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Usuarios"" 
                (""Id"", ""Nombre"", ""Apellidos"", ""TipoDocumento"", ""Documento"", ""Pais"", ""Provincia"", ""Localidad"", 
                 ""Direccion"", ""CodigoPostal"", ""Telefono1"", ""Telefono2"", ""Email"", ""FechaAlta"", ""IsActive"", ""CreatedAt"")
                VALUES 
                (@Id, @Nombre, @Apellidos, @TipoDocumento, @Documento, @Pais, @Provincia, @Localidad,
                 @Direccion, @CP, @Tel1, @Tel2, @Email, @FechaAlta, @Activo, @FechaAlta)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Usuario"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdUsuario"]);
                        cmd.Parameters.AddWithValue("@Nombre", reader["Nombre"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Apellidos", reader["Apellidos"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@TipoDocumento", reader["TipoDocumento"]?.ToString() ?? "DNI");
                        cmd.Parameters.AddWithValue("@Documento", reader["Documento"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Pais", OrNull(reader["Pais"]));
                        cmd.Parameters.AddWithValue("@Provincia", OrNull(reader["Provincia"]));
                        cmd.Parameters.AddWithValue("@Localidad", OrNull(reader["Localidad"]));
                        cmd.Parameters.AddWithValue("@Direccion", OrNull(reader["Direccion"]));
                        cmd.Parameters.AddWithValue("@CP", OrNull(reader["CP"]));
                        cmd.Parameters.AddWithValue("@Tel1", OrNull(reader["Telefono1"]));
                        cmd.Parameters.AddWithValue("@Tel2", OrNull(reader["Telefono2"]));
                        cmd.Parameters.AddWithValue("@Email", OrNull(reader["Email"]));
                        cmd.Parameters.AddWithValue("@FechaAlta", (DateTime)reader["FechaAlta"]);
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Usuarios", count);
        }

        // ════════════════════════════════════════════
        // LOGINS (passwords en texto plano → SHA-256)
        // ════════════════════════════════════════════
        static void MigrateLogins(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Logins"" 
                (""Id"", ""UsuarioId"", ""NombreUsuario"", ""PasswordHash"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @UsuarioId, @Nombre, @Hash, TRUE, @Now)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Login"))
            {
                while (reader.Read())
                {
                    string plainPass = reader["Password"]?.ToString() ?? "";
                    string hash = HashPassword(plainPass);

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdLogin"]);
                        cmd.Parameters.AddWithValue("@UsuarioId", (long)reader["IdUsuario"]);
                        cmd.Parameters.AddWithValue("@Nombre", reader["NombreUsuario"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Hash", hash);
                        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Logins", count);
        }

        // ════════════════════════════════════════════
        // SOCIOS
        // ════════════════════════════════════════════
        static void MigrateSocios(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Socios"" 
                (""Id"", ""NumSocio"", ""Codigo"", ""Nombre"", ""PrimerApellido"", ""SegundoApellido"",
                 ""TipoDocumento"", ""Documento"", ""Pais"", ""Provincia"", ""Localidad"", ""Direccion"",
                 ""CodigoPostal"", ""Telefono"", ""Email"", ""FechaNacimiento"", ""FechaAlta"",
                 ""FotoUrl"", ""FotoAnversoDniUrl"", ""FotoReversoDniUrl"",
                 ""Estrellas"", ""ConsumicionMaximaMensual"", ""EsTerapeutica"", ""EsExento"", ""PagoConTarjeta"",
                 ""Comentario"", ""IsActive"", ""CreatedAt"")
                VALUES 
                (@Id, @NumSocio, @Codigo, @Nombre, @Ap1, @Ap2,
                 @TipoDoc, @Doc, @Pais, @Prov, @Loc, @Dir,
                 @CP, @Tel, @Email, @FechaNac, @FechaAlta,
                 @Foto, @FotoDniA, @FotoDniR,
                 @Estrellas, @ConsMax, @Terap, @Exento, @Tarjeta,
                 @Comentario, @Activo, @FechaAlta)
                ON CONFLICT (""Id"") DO NOTHING";

            var codigoToId = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            var referidoPorMap = new Dictionary<long, string>();

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Socio"))
            {
                while (reader.Read())
                {
                    long id = (long)reader["IdSocio"];
                    string codigo = CleanText(reader["Codigo"]) ?? "";
                    string referidoPor = CleanText(reader["ReferidoPor"]);

                    if (!string.IsNullOrWhiteSpace(codigo))
                        codigoToId[codigo] = id;

                    if (!string.IsNullOrWhiteSpace(referidoPor))
                        referidoPorMap[id] = referidoPor;

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", id);
                        cmd.Parameters.AddWithValue("@NumSocio", (int)reader["NumSocio"]);
                        cmd.Parameters.AddWithValue("@Codigo", codigo);
                        cmd.Parameters.AddWithValue("@Nombre", CleanText(reader["Nombre"]));
                        cmd.Parameters.AddWithValue("@Ap1", CleanText(reader["PrimerApellido"]));
                        cmd.Parameters.AddWithValue("@Ap2", OrNull(CleanText(reader["SegundoApellido"])));
                        cmd.Parameters.AddWithValue("@TipoDoc", CleanText(reader["TipoDocumento"]) ?? "DNI");
                        cmd.Parameters.AddWithValue("@Doc", CleanText(reader["Documento"])) ;
                        cmd.Parameters.AddWithValue("@Pais", OrNull(CleanText(reader["Pais"])));
                        cmd.Parameters.AddWithValue("@Prov", OrNull(CleanText(reader["Provincia"])));
                        cmd.Parameters.AddWithValue("@Loc", OrNull(CleanText(reader["Localidad"])));
                        cmd.Parameters.AddWithValue("@Dir", OrNull(CleanText(reader["Direccion"])));
                        cmd.Parameters.AddWithValue("@CP", OrNull(CleanText(reader["CP"])));
                        cmd.Parameters.AddWithValue("@Tel", OrNull(CleanText(reader["Telefono"])));
                        cmd.Parameters.AddWithValue("@Email", OrNull(CleanText(reader["Email"])));
                        cmd.Parameters.AddWithValue("@FechaNac", OrNullDate(reader["FechaCumple"]));
                        cmd.Parameters.AddWithValue("@FechaAlta", (DateTime)reader["FechaAlta"]);
                        cmd.Parameters.AddWithValue("@Foto", PrefixPath(CleanText(reader["Foto"]), "/resources/fotos/"));
                        cmd.Parameters.AddWithValue("@FotoDniA", PrefixPath(CleanText(reader["FotoAnversoDNI"]), "/resources/documentos/"));
                        cmd.Parameters.AddWithValue("@FotoDniR", PrefixPath(CleanText(reader["FotoReversoDNI"]), "/resources/documentos/"));
                        cmd.Parameters.AddWithValue("@Estrellas", (short)reader["NEstrellas"]);
                        cmd.Parameters.AddWithValue("@ConsMax", (int)reader["ConsumicionMaxima"]);
                        cmd.Parameters.AddWithValue("@Terap", (bool)reader["Terapeutica"]);
                        cmd.Parameters.AddWithValue("@Exento", (bool)reader["Exento"]);
                        cmd.Parameters.AddWithValue("@Tarjeta", (bool)reader["PagoTarjeta"]);
                        cmd.Parameters.AddWithValue("@Comentario", OrNull(CleanText(reader["Comentario"])));
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }

            // Resolver ReferidoPor: código→SocioId
            int refs = 0;
            foreach (var kv in referidoPorMap)
            {
                if (codigoToId.TryGetValue(kv.Value, out long refId))
                {
                    ExecutePg(pg, @"UPDATE ""Socios"" SET ""ReferidoPorSocioId"" = @ref WHERE ""Id"" = @id",
                        ("@ref", (object)refId), ("@id", (object)kv.Key));
                    refs++;
                }
            }

            LogTable("Socios", count);
            if (refs > 0) Console.WriteLine($"    └─ {refs} referencias 'ReferidoPor' resueltas");
        }

        // ════════════════════════════════════════════
        // SOCIO DETALLES (TablaAuxiliarSocio)
        // ════════════════════════════════════════════
        static void MigrateSocioDetalles(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""SocioDetalles"" 
                (""SocioId"", ""CuotaFechaProxima"", ""ConsumicionDelMes"", ""AportacionDelDia"",
                 ""FechaUltimaConsumicion"", ""FechaUltimaAportacion"", ""ExentoCuota"", ""DebeCuota"", ""Aprovechable"")
                VALUES 
                (@SocioId, @CuotaProx, @ConsMes, @AporDia,
                 @FechaCons, @FechaApor, @Exento, @Debe, @Aprove)
                ON CONFLICT (""SocioId"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM TablaAuxiliarSocio"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@SocioId", (long)reader["IdSocio"]);
                        cmd.Parameters.AddWithValue("@CuotaProx", OrNullDate(reader["CuotaFechaProxima"]));
                        cmd.Parameters.AddWithValue("@ConsMes", ToDecimal(reader["ConsumicionDelMes"]));
                        cmd.Parameters.AddWithValue("@AporDia", OrNullDecimal(reader["AportacionDelDia"]));
                        cmd.Parameters.AddWithValue("@FechaCons", OrNullDate(reader["FechaUltimaConsumicion"]));
                        cmd.Parameters.AddWithValue("@FechaApor", OrNullDate(reader["FechaUltimaAportacion"]));
                        cmd.Parameters.AddWithValue("@Exento", (bool)reader["ExentoCuota"]);
                        cmd.Parameters.AddWithValue("@Debe", (bool)reader["DebeCuota"]);
                        cmd.Parameters.AddWithValue("@Aprove", ToDecimal(reader["Aprovechable"]));
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("SocioDetalles", count);
        }

        // ════════════════════════════════════════════
        // USERLOGINS (passwords en texto plano → SHA-256)
        // ════════════════════════════════════════════
        static void MigrateUserLogins(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""UserLogins"" 
                (""Id"", ""Username"", ""PasswordHash"", ""Rol"", ""FechaAlta"", ""SocioId"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @User, @Hash, @Rol, @FechaAlta, @SocioId, TRUE, @FechaAlta)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM UserLogin"))
            {
                while (reader.Read())
                {
                    string plainPass = reader["Userpassword"]?.ToString() ?? "";
                    string hash = HashPassword(plainPass);
                    string mode = reader["Usermode"]?.ToString() ?? "socio";
                    string rol = mode.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "admin" : "socio";

                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdUserLogin"]);
                        cmd.Parameters.AddWithValue("@User", reader["Username"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Hash", hash);
                        cmd.Parameters.AddWithValue("@Rol", rol);
                        cmd.Parameters.AddWithValue("@FechaAlta", (DateTime)reader["FechaAlta"]);
                        cmd.Parameters.AddWithValue("@SocioId", (long)reader["IdSocio"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("UserLogins", count);
        }

        // ════════════════════════════════════════════
        // FAMILIAS
        // ════════════════════════════════════════════
        static void MigrateFamilias(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Familias"" (""Id"", ""Nombre"", ""Descuento"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @Nombre, @Desc, @Activo, @Now)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Familia"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdFamilia"]);
                        cmd.Parameters.AddWithValue("@Nombre", reader["Nombre"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Desc", OrNullInt(reader["Descuento"]));
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Familias", count);
        }

        // ════════════════════════════════════════════
        // ARTICULOS
        // ════════════════════════════════════════════
        static void MigrateArticulos(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Articulos"" 
                (""Id"", ""FamiliaId"", ""Nombre"", ""Descripcion"", ""Precio"", ""Cantidad1"", ""Cantidad2"", ""Cantidad3"", ""Cantidad4"",
                 ""EsDecimal"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @FamId, @Nombre, @Desc, @Precio, @C1, @C2, @C3, @C4, @EsDec, @Activo, @Now)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Articulo"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdArticulo"]);
                        cmd.Parameters.AddWithValue("@FamId", (long)reader["IdFamilia"]);
                        cmd.Parameters.AddWithValue("@Nombre", reader["Nombre"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Desc", OrNull(reader["Descripcion"]));
                        cmd.Parameters.AddWithValue("@Precio", ToDecimal(reader["Precio"]));
                        cmd.Parameters.AddWithValue("@C1", ToDecimal(reader["Cantidad1"]));
                        cmd.Parameters.AddWithValue("@C2", OrNullDecimal(reader["Cantidad2"]));
                        cmd.Parameters.AddWithValue("@C3", OrNullDecimal(reader["Cantidad3"]));
                        cmd.Parameters.AddWithValue("@C4", OrNullDecimal(reader["Cantidad4"]));
                        cmd.Parameters.AddWithValue("@EsDec", (bool)reader["Decimal"]);
                        cmd.Parameters.AddWithValue("@Activo", (bool)reader["Activo"]);
                        cmd.Parameters.AddWithValue("@Now", DateTime.UtcNow);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Articulos", count);
        }

        // ════════════════════════════════════════════
        // APORTACIONES
        // ════════════════════════════════════════════
        static void MigrateAportaciones(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Aportaciones"" 
                (""Id"", ""SocioId"", ""UsuarioId"", ""CantidadAportada"", ""Fecha"", ""Codigo"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @UsuarioId, @Cantidad, @Fecha, @Codigo, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Aportacion"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdAportacion"]);
                        cmd.Parameters.AddWithValue("@SocioId", (long)reader["IdSocio"]);
                        cmd.Parameters.AddWithValue("@UsuarioId", (long)reader["IdUsuario"]);
                        cmd.Parameters.AddWithValue("@Cantidad", ToDecimal(reader["CantidadAportada"]));
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                        cmd.Parameters.AddWithValue("@Codigo", reader["Codigo"]?.ToString() ?? "");
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Aportaciones", count);
        }

        // ════════════════════════════════════════════
        // RETIRADAS
        // ════════════════════════════════════════════
        static void MigrateRetiradas(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Retiradas"" 
                (""Id"", ""SocioId"", ""ArticuloId"", ""UsuarioId"", ""PrecioArticulo"", ""Cantidad"", ""Total"", ""FirmaUrl"", ""Fecha"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @ArtId, @UsrId, @Precio, @Cant, @Total, @Firma, @Fecha, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Retirada"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdRetirada"]);
                        cmd.Parameters.AddWithValue("@SocioId", (long)reader["IdSocio"]);
                        cmd.Parameters.AddWithValue("@ArtId", (long)reader["IdArticulo"]);
                        cmd.Parameters.AddWithValue("@UsrId", (long)reader["IdUsuario"]);
                        cmd.Parameters.AddWithValue("@Precio", ToDecimal(reader["PrecioArticulo"]));
                        cmd.Parameters.AddWithValue("@Cant", ToDecimal(reader["Cantidad"]));
                        cmd.Parameters.AddWithValue("@Total", ToDecimal(reader["Total"]));
                        cmd.Parameters.AddWithValue("@Firma", PrefixPath(CleanText(reader["Firma"]), "/resources/firmas/"));
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Retiradas", count);
        }

        // ════════════════════════════════════════════
        // CUOTAS
        // ════════════════════════════════════════════
        static void MigrateCuotas(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Cuotas"" 
                (""Id"", ""SocioId"", ""Fecha"", ""CantidadCuota"", ""Periodo"", ""UsuarioId"", ""FechaAnterior"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @Fecha, @Cant, @Per, @UsrId, @FechaAnt, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Cuota"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdCuota"]);
                        cmd.Parameters.AddWithValue("@SocioId", (long)reader["IdSocio"]);
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                        cmd.Parameters.AddWithValue("@Cant", (int)reader["CantidadCuota"]);
                        cmd.Parameters.AddWithValue("@Per", (int)reader["Periodo"]);
                        cmd.Parameters.AddWithValue("@UsrId", (long)reader["IdUsuario"]);
                        cmd.Parameters.AddWithValue("@FechaAnt", (DateTime)reader["FechaAnterior"]);
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Cuotas", count);
        }

        // ════════════════════════════════════════════
        // ACCESOS
        // ════════════════════════════════════════════
        static void MigrateAccesos(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""Accesos"" 
                (""Id"", ""SocioId"", ""TipoAcceso"", ""FechaHora"", ""Turno"", ""Accion"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @SocioId, @Tipo, @Fecha, @Turno, @Accion, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            using (var reader = ReadCe(ce, "SELECT * FROM Acceso"))
            {
                while (reader.Read())
                {
                    using (var cmd = new NpgsqlCommand(sql, pg))
                    {
                        cmd.Parameters.AddWithValue("@Id", (long)reader["IdAcceso"]);
                        cmd.Parameters.AddWithValue("@SocioId", (long)reader["IdSocio"]);
                        cmd.Parameters.AddWithValue("@Tipo", reader["TipoAcceso"]?.ToString() ?? "");
                        cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["FechaYHora"]);
                        cmd.Parameters.AddWithValue("@Turno", OrNull(reader["Turno"]));
                        cmd.Parameters.AddWithValue("@Accion", reader["Accion"]?.ToString() ?? "");
                        cmd.ExecuteNonQuery();
                        count++;
                    }
                }
            }
            LogTable("Accesos", count);
        }

        // ════════════════════════════════════════════
        // BORRADOS → RegistrosBorrados
        // ════════════════════════════════════════════
        static void MigrateBorrados(SqlCeConnection ce, NpgsqlConnection pg)
        {
            var sql = @"INSERT INTO ""RegistrosBorrados"" 
                (""Id"", ""Fecha"", ""Tipo"", ""Descripcion"", ""SocioId"", ""FamiliaId"", ""UsuarioId"", ""ArticuloId"", ""CuotaId"",
                 ""Cantidad"", ""Total"", ""RealizadoPorUsuarioId"", ""IsActive"", ""CreatedAt"")
                VALUES (@Id, @Fecha, @Tipo, @Desc, @SocioId, @FamId, @UsrId, @ArtId, @CuoId,
                 @Cant, @Total, @Realizado, TRUE, @Fecha)
                ON CONFLICT (""Id"") DO NOTHING";

            int count = 0;
            try
            {
                using (var reader = ReadCe(ce, "SELECT * FROM Borrado"))
                {
                    while (reader.Read())
                    {
                        using (var cmd = new NpgsqlCommand(sql, pg))
                        {
                            cmd.Parameters.AddWithValue("@Id", (long)reader["IdBorrado"]);
                            cmd.Parameters.AddWithValue("@Fecha", (DateTime)reader["Fecha"]);
                            cmd.Parameters.AddWithValue("@Tipo", reader["Tipo"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@Desc", reader["Descripcion"]?.ToString() ?? "");
                            cmd.Parameters.AddWithValue("@SocioId", OrNullLong(reader["IdSocio"]));
                            cmd.Parameters.AddWithValue("@FamId", OrNullLong(reader["IdFamilia"]));
                            cmd.Parameters.AddWithValue("@UsrId", OrNullLong(reader["IdUsuario"]));
                            cmd.Parameters.AddWithValue("@ArtId", OrNullLong(reader["IdArticulo"]));
                            cmd.Parameters.AddWithValue("@CuoId", OrNullLong(reader["IdCuota"]));
                            cmd.Parameters.AddWithValue("@Cant", OrNullDecimal(reader["Cantidad"]));
                            cmd.Parameters.AddWithValue("@Total", OrNullDecimal(reader["Total"]));
                            cmd.Parameters.AddWithValue("@Realizado", (long)(int)reader["RealizadoPor"]);
                            cmd.ExecuteNonQuery();
                            count++;
                        }
                    }
                }
            }
            catch (SqlCeException)
            {
                Console.WriteLine("  ⚠ Tabla 'Borrado' no encontrada en .sdf, saltando...");
            }
            LogTable("RegistrosBorrados", count);
        }

        // ════════════════════════════════════════════
        // Ajustar secuencias SERIAL tras insertar con IDs explícitos
        // ════════════════════════════════════════════
        static void ResetSequences(NpgsqlConnection pg)
        {
            Console.WriteLine();
            Console.WriteLine("  Ajustando secuencias SERIAL...");

            string[] tables = { "Usuarios", "Logins", "Socios", "UserLogins", "Familias",
                "Articulos", "Aportaciones", "Retiradas", "Cuotas", "Accesos", "RefreshTokens", "RegistrosBorrados" };

            foreach (var t in tables)
            {
                try
                {
                    var sql = $@"SELECT setval(pg_get_serial_sequence('""{t}""', 'Id'), COALESCE(MAX(""Id""), 1)) FROM ""{t}""";
                    ExecutePg(pg, sql);
                }
                catch { /* tabla vacía o sin secuencia, ignorar */ }
            }
            Console.WriteLine("  ✓ Secuencias ajustadas");
        }

        // ════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════
        static SqlCeDataReader ReadCe(SqlCeConnection conn, string sql)
        {
            var cmd = new SqlCeCommand(sql, conn);
            return cmd.ExecuteReader();
        }

        static void ExecutePg(NpgsqlConnection conn, string sql, params (string name, object value)[] pars)
        {
            using (var cmd = new NpgsqlCommand(sql, conn))
            {
                foreach (var p in pars)
                    cmd.Parameters.AddWithValue(p.name, p.value);
                cmd.ExecuteNonQuery();
            }
        }

        static string HashPassword(string password)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(bytes);
            }
        }

        static object OrNull(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            string s = val.ToString();
            return string.IsNullOrWhiteSpace(s) ? (object)DBNull.Value : s;
        }

        static object OrNullDate(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return (DateTime)val;
        }

        static object OrNullDecimal(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return Convert.ToDecimal(val);
        }

        static object OrNullInt(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return Convert.ToInt32(val);
        }

        static object OrNullLong(object val)
        {
            if (val == null || val == DBNull.Value) return DBNull.Value;
            return Convert.ToInt64(val);
        }

        // Elimina caracteres fuera de WIN1252 (ej. emojis) para evitar errores de codificación
        static string CleanText(object val)
        {
            if (val == null || val == DBNull.Value) return null;
            var s = val.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                // 0x00-0xFF rango básico; descarta sustitutos/emoji
                if (ch <= 0xFF) sb.Append(ch);
            }
            var result = sb.ToString().Trim();
            return result.Length == 0 ? null : result;
        }

        // Prefija un nombre de archivo suelto con la ruta del servidor (ej. "foto.jpg" → "/resources/fotos/foto.jpg")
        static object PrefixPath(string val, string prefix)
        {
            if (string.IsNullOrWhiteSpace(val)) return DBNull.Value;
            // Si ya tiene ruta absoluta, dejarlo
            if (val.StartsWith("/")) return val;
            return prefix + val;
        }

        static decimal ToDecimal(object val) => Convert.ToDecimal(val ?? 0);

        static void LogTable(string table, int count)
        {
            _totalMigrated += count;
            Console.ForegroundColor = count > 0 ? ConsoleColor.Cyan : ConsoleColor.DarkGray;
            Console.WriteLine($"  ✓ {table,-25} → {count,6} registros");
            Console.ResetColor();
        }
    }
}
