using MexClub.Application.Common;
using MexClub.Application.DTOs.Productos;

namespace MexClub.Application.Interfaces;

public interface IFamiliaService
{
    Task<ServiceResult<IReadOnlyList<FamiliaDto>>> GetAllAsync(bool? soloActivas = null, CancellationToken ct = default);
    Task<ServiceResult<FamiliaDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<FamiliaDto>> CreateAsync(CreateFamiliaRequest request, CancellationToken ct = default);
    Task<ServiceResult<FamiliaDto>> UpdateAsync(UpdateFamiliaRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeactivateAsync(long id, CancellationToken ct = default);
}

public interface IArticuloService
{
    Task<ServiceResult<IReadOnlyList<ArticuloDto>>> GetAllAsync(bool? soloActivos = null, long? familiaId = null, CancellationToken ct = default);
    Task<ServiceResult<ArticuloDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<ArticuloDto>> CreateAsync(CreateArticuloRequest request, CancellationToken ct = default);
    Task<ServiceResult<ArticuloDto>> UpdateAsync(UpdateArticuloRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeactivateAsync(long id, CancellationToken ct = default);
}
