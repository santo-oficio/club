namespace MexClub.Domain.Entities;

public class Login : BaseEntity
{
    public long UsuarioId { get; set; }
    public string NombreUsuario { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    // Navigation
    public Usuario Usuario { get; set; } = null!;
}
