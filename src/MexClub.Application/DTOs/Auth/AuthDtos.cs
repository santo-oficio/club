namespace MexClub.Application.DTOs.Auth;

public record LoginRequest(string Username, string Password);

public record LoginResponse(
    string Token,
    string RefreshToken,
    DateTime Expiration,
    string Username,
    string Rol,
    long UserId
);

public record RefreshTokenRequest(string Token, string RefreshToken);

public record ChangePasswordRequest(string OldPassword, string NewPassword);
