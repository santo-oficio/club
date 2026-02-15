namespace MexClub.Domain.Entities;

public class SocioDetalle
{
    public long SocioId { get; set; }
    public DateTime? CuotaFechaProxima { get; set; }
    public decimal ConsumicionDelMes { get; set; }
    public decimal? AportacionDelDia { get; set; }
    public DateTime? FechaUltimaConsumicion { get; set; }
    public DateTime? FechaUltimaAportacion { get; set; }
    public bool ExentoCuota { get; set; }
    public bool DebeCuota { get; set; }
    public decimal Aprovechable { get; set; }

    // Navigation property
    public Socio Socio { get; set; } = null!;
}
