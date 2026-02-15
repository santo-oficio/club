namespace MexClub.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expiration { get; set; }
    public bool IsRevoked { get; set; }
    public long? UserLoginId { get; set; }
    public long? LoginId { get; set; }

    // Navigation
    public UserLogin? UserLogin { get; set; }
    public Login? Login { get; set; }
}
