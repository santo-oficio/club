namespace MexClub.Application.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(long userId, string username, string role);
    string GenerateRefreshToken();
    (bool isValid, long userId, string username, string role) ValidateToken(string token);
}
