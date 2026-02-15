using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using MexClub.Application.Common;
using MexClub.Application.DTOs.Auth;
using MexClub.Application.Interfaces;
using MexClub.Domain.Entities;
using MexClub.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MexClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IRepository<UserLogin> _userLoginRepo;
    private readonly IRepository<Login> _loginRepo;
    private readonly IRepository<RefreshToken> _refreshTokenRepo;
    private readonly ITokenService _tokenService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IRepository<UserLogin> userLoginRepo,
        IRepository<Login> loginRepo,
        IRepository<RefreshToken> refreshTokenRepo,
        ITokenService tokenService,
        IConfiguration configuration,
        ILogger<AuthController> logger)
    {
        _userLoginRepo = userLoginRepo;
        _loginRepo = loginRepo;
        _refreshTokenRepo = refreshTokenRepo;
        _tokenService = tokenService;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ServiceResult<LoginResponse>>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var userLogin = await _userLoginRepo.FirstOrDefaultAsync(
            u => u.Username == request.Username && u.IsActive, ct);

        if (userLogin == null)
        {
            var adminLogin = await _loginRepo.FirstOrDefaultAsync(
                l => l.NombreUsuario == request.Username && l.IsActive, ct);

            if (adminLogin == null || !VerifyPassword(request.Password, adminLogin.PasswordHash))
            {
                _logger.LogWarning("Login fallido para usuario: {Username}", request.Username);
                return Unauthorized(ServiceResult.Fail("Credenciales inválidas."));
            }

            var adminToken = _tokenService.GenerateAccessToken(adminLogin.Id, adminLogin.NombreUsuario, "admin");
            var adminRefresh = await CreateRefreshTokenAsync(loginId: adminLogin.Id, ct: ct);

            return Ok(ServiceResult<LoginResponse>.Ok(new LoginResponse(
                adminToken, adminRefresh, GetTokenExpiration(), adminLogin.NombreUsuario, "admin", adminLogin.Id)));
        }

        if (!VerifyPassword(request.Password, userLogin.PasswordHash))
        {
            _logger.LogWarning("Login fallido para socio: {Username}", request.Username);
            return Unauthorized(ServiceResult.Fail("Credenciales inválidas."));
        }

        var token = _tokenService.GenerateAccessToken(userLogin.Id, userLogin.Username, userLogin.Rol);
        var refreshToken = await CreateRefreshTokenAsync(userLoginId: userLogin.Id, ct: ct);

        _logger.LogInformation("Login exitoso: {Username}", request.Username);

        return Ok(ServiceResult<LoginResponse>.Ok(new LoginResponse(
            token, refreshToken, GetTokenExpiration(), userLogin.Username, userLogin.Rol, userLogin.Id)));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ServiceResult<LoginResponse>>> Refresh([FromBody] RefreshTokenRequest request, CancellationToken ct)
    {
        var (isValid, userId, username, role) = _tokenService.ValidateToken(request.Token);
        if (!isValid)
            return Unauthorized(ServiceResult.Fail("Token inválido."));

        var storedToken = await _refreshTokenRepo.FirstOrDefaultAsync(
            r => r.Token == request.RefreshToken && !r.IsRevoked && r.Expiration > DateTime.UtcNow, ct);

        if (storedToken == null)
            return Unauthorized(ServiceResult.Fail("Refresh token inválido o expirado."));

        storedToken.Expiration = EnsureUtc(storedToken.Expiration);
        storedToken.IsRevoked = true;
        await _refreshTokenRepo.UpdateAsync(storedToken, ct);

        var newAccessToken = _tokenService.GenerateAccessToken(userId, username, role);
        var newRefreshToken = await CreateRefreshTokenAsync(
            userLoginId: storedToken.UserLoginId,
            loginId: storedToken.LoginId,
            ct: ct);

        return Ok(ServiceResult<LoginResponse>.Ok(new LoginResponse(
            newAccessToken, newRefreshToken, GetTokenExpiration(), username, role, userId)));
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ServiceResult>> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        if (userIdClaim == null || !long.TryParse(userIdClaim, out var userId))
            return Unauthorized(ServiceResult.Fail("Usuario no identificado."));

        var role = User.FindFirst(ClaimTypes.Role)?.Value ?? "";

        if (role == "admin")
        {
            var login = await _loginRepo.GetByIdAsync(userId, ct);
            if (login == null || !VerifyPassword(request.OldPassword, login.PasswordHash))
                return BadRequest(ServiceResult.Fail("Contraseña actual incorrecta."));

            login.PasswordHash = HashPassword(request.NewPassword);
            await _loginRepo.UpdateAsync(login, ct);
        }
        else
        {
            var userLogin = await _userLoginRepo.GetByIdAsync(userId, ct);
            if (userLogin == null || !VerifyPassword(request.OldPassword, userLogin.PasswordHash))
                return BadRequest(ServiceResult.Fail("Contraseña actual incorrecta."));

            userLogin.PasswordHash = HashPassword(request.NewPassword);
            await _userLoginRepo.UpdateAsync(userLogin, ct);
        }

        return Ok(ServiceResult.Ok("Contraseña cambiada exitosamente."));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ServiceResult>> Logout([FromBody] string refreshToken, CancellationToken ct)
    {
        var token = await _refreshTokenRepo.FirstOrDefaultAsync(
            r => r.Token == refreshToken && !r.IsRevoked, ct);

        if (token != null)
        {
            token.IsRevoked = true;
            await _refreshTokenRepo.UpdateAsync(token, ct);
        }

        return Ok(ServiceResult.Ok("Sesión cerrada."));
    }

    private async Task<string> CreateRefreshTokenAsync(long? userLoginId = null, long? loginId = null, CancellationToken ct = default)
    {
        var days = int.Parse(_configuration["Jwt:RefreshTokenExpirationDays"] ?? "7");
        var refreshToken = new RefreshToken
        {
            Token = _tokenService.GenerateRefreshToken(),
            Expiration = EnsureUtc(DateTime.UtcNow.AddDays(days)),
            UserLoginId = userLoginId,
            LoginId = loginId
        };

        await _refreshTokenRepo.AddAsync(refreshToken, ct);
        return refreshToken.Token;
    }

    private DateTime GetTokenExpiration()
    {
        var minutes = int.Parse(_configuration["Jwt:ExpirationMinutes"] ?? "60");
        return DateTime.UtcNow.AddMinutes(minutes);
    }

    private static DateTime EnsureUtc(DateTime dt)
    {
        return dt.Kind == DateTimeKind.Utc
            ? dt
            : DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(bytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }
}
