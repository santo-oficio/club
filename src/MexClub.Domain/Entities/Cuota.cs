namespace MexClub.Domain.Entities;

public class Cuota : BaseEntity
{
    public long SocioId { get; set; }
    public DateTime Fecha { get; set; }
    public int CantidadCuota { get; set; }
    public PeriodoCuota Periodo { get; set; }
    public long UsuarioId { get; set; }
    public DateTime FechaAnterior { get; set; }

    // Navigation
    public Socio Socio { get; set; } = null!;
    public Usuario Usuario { get; set; } = null!;
}

public enum PeriodoCuota
{
    Mensual = 1,
    Anual = 12
}
