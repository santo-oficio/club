# Use the migration tool's SqlCe DLL to query the old DB
Add-Type -Path "C:\Proyectos personales\Mexclub cliente y servidor\scripts\migration\MigradorDatos\bin\Release\net481\System.Data.SqlServerCe.dll"

$sdfPath = "C:\Server\ClubDB.sdf"
if (-not (Test-Path $sdfPath)) {
    Write-Host "ERROR: No se encuentra $sdfPath" -ForegroundColor Red
    exit 1
}

$conn = New-Object System.Data.SqlServerCe.SqlCeConnection("Data Source=$sdfPath;Max Database Size=4091;")
$conn.Open()

# List columns
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'Socio' ORDER BY ORDINAL_POSITION"
$reader = $cmd.ExecuteReader()
Write-Host "=== Columnas de tabla Socio ===" -ForegroundColor Cyan
while ($reader.Read()) { Write-Host "  $($reader[0])" }
$reader.Close()

# Check first 15 rows
$cmd2 = $conn.CreateCommand()
$cmd2.CommandText = "SELECT TOP(15) IdSocio, NumSocio, Codigo, Documento, Nombre, PrimerApellido FROM Socio ORDER BY IdSocio"
$reader2 = $cmd2.ExecuteReader()
Write-Host "`n=== Primeros 15 socios: Codigo vs Documento ===" -ForegroundColor Cyan
while ($reader2.Read()) {
    Write-Host ("  Id={0} Num={1} Codigo=[{2}] Doc=[{3}] {4} {5}" -f $reader2["IdSocio"], $reader2["NumSocio"], $reader2["Codigo"], $reader2["Documento"], $reader2["Nombre"], $reader2["PrimerApellido"])
}
$reader2.Close()
$conn.Close()
