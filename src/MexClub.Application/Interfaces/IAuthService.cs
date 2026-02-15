using MexClub.Application.Common;
using MexClub.Application.DTOs.Auth;

namespace MexClub.Application.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<ServiceResult<LoginResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken ct = default);
    Task<ServiceResult> ChangePasswordAsync(long userId, string userType, ChangePasswordRequest request, CancellationToken ct = default);
    Task<ServiceResult> RevokeRefreshTokenAsync(string refreshToken, CancellationToken ct = default);
}
