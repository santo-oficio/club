namespace MexClub.Domain.Entities;

public class Usuario : BaseEntity
{
    public string Nombre { get; set; } = string.Empty;
    public string Apellidos { get; set; } = string.Empty;
    public string TipoDocumento { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string? Pais { get; set; }
    public string? Provincia { get; set; }
    public string? Localidad { get; set; }
    public string? Direccion { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Telefono1 { get; set; }
    public string? Telefono2 { get; set; }
    public string? Email { get; set; }
    public DateTime FechaAlta { get; set; }

    // Navigation
    public Login? Login { get; set; }

    public string NombreCompleto => $"{Nombre} {Apellidos}".Trim();
}
