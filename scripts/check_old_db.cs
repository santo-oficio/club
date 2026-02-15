// Quick script to check old DB columns - run with migration tool or csx
// Just checking what Codigo vs Documento look like in the old .sdf
using System;
using System.Data.SqlServerCe;

var sdfPath = @"C:\Server\ClubDB.sdf";
var ceConn = $"Data Source={sdfPath};Max Database Size=4091;";

using var conn = new SqlCeConnection(ceConn);
conn.Open();

// First: list all columns in Socio table
using var schemaCmd = new SqlCeCommand("SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Socio' ORDER BY ORDINAL_POSITION", conn);
using var schemaReader = schemaCmd.ExecuteReader();
Console.WriteLine("=== Columns in Socio table ===");
while (schemaReader.Read()) Console.WriteLine("  " + schemaReader[0]);

// Then check first 15 rows
Console.WriteLine("\n=== First 15 socios: Codigo vs Documento ===");
using var cmd = new SqlCeCommand("SELECT TOP(15) IdSocio, NumSocio, Codigo, Documento, Nombre, PrimerApellido FROM Socio ORDER BY IdSocio", conn);
using var reader = cmd.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"  Id={reader["IdSocio"]} Num={reader["NumSocio"]} Codigo=[{reader["Codigo"]}] Documento=[{reader["Documento"]}] Nombre={reader["Nombre"]} {reader["PrimerApellido"]}");
}
