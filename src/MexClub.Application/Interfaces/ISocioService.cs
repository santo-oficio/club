using MexClub.Application.Common;
using MexClub.Application.DTOs.Socios;

namespace MexClub.Application.Interfaces;

public interface ISocioService
{
    Task<ServiceResult<SocioDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<SocioDto>> GetByCodigoOrDocumentoAsync(string code, CancellationToken ct = default);
    Task<ServiceResult<PagedResult<SocioDto>>> GetAllAsync(PaginationParams pagination, bool? soloActivos = null, CancellationToken ct = default);
    Task<ServiceResult<SocioDto>> CreateAsync(CreateSocioRequest request, CancellationToken ct = default);
    Task<ServiceResult<SocioDto>> UpdateAsync(UpdateSocioRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeactivateAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<IReadOnlyList<SocioDto>>> GetReferidosAsync(string code, CancellationToken ct = default);
    Task<ServiceResult<string>> UploadFotoAsync(long socioId, string tipoFoto, byte[] fileBytes, string fileName, CancellationToken ct = default);
}
