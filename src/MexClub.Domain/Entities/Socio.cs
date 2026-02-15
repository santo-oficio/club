namespace MexClub.Domain.Entities;

public class Socio : BaseEntity
{
    public int NumSocio { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public long? ReferidoPorSocioId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string PrimerApellido { get; set; } = string.Empty;
    public string? SegundoApellido { get; set; }
    public string TipoDocumento { get; set; } = string.Empty;
    public string Documento { get; set; } = string.Empty;
    public string? Pais { get; set; }
    public string? Provincia { get; set; }
    public string? Localidad { get; set; }
    public string? Direccion { get; set; }
    public string? CodigoPostal { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public DateTime FechaAlta { get; set; }
    public string? FotoUrl { get; set; }
    public string? FotoAnversoDniUrl { get; set; }
    public string? FotoReversoDniUrl { get; set; }
    public short Estrellas { get; set; }
    public int ConsumicionMaximaMensual { get; set; }
    public bool EsTerapeutica { get; set; }
    public bool EsExento { get; set; }
    public bool PagoConTarjeta { get; set; }
    public string? Comentario { get; set; }

    // Navigation properties
    public Socio? ReferidoPor { get; set; }
    public ICollection<Socio> Referidos { get; set; } = new List<Socio>();
    public SocioDetalle? Detalle { get; set; }
    public ICollection<Acceso> Accesos { get; set; } = new List<Acceso>();
    public ICollection<Aportacion> Aportaciones { get; set; } = new List<Aportacion>();
    public ICollection<Retirada> Retiradas { get; set; } = new List<Retirada>();
    public ICollection<Cuota> Cuotas { get; set; } = new List<Cuota>();
    public UserLogin? UserLogin { get; set; }

    public string NombreCompleto => $"{Nombre} {PrimerApellido} {SegundoApellido}".Trim();
}
