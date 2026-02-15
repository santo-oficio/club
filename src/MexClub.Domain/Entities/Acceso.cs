namespace MexClub.Domain.Entities;

public class Acceso : BaseEntity
{
    public long SocioId { get; set; }
    public TipoAcceso TipoAcceso { get; set; }
    public DateTime FechaHora { get; set; }
    public string? Turno { get; set; }
    public AccionAcceso Accion { get; set; }

    // Navigation
    public Socio Socio { get; set; } = null!;
}

public enum TipoAcceso
{
    Entrada,
    Salida
}

public enum AccionAcceso
{
    Ok,
    SinFichar
}
