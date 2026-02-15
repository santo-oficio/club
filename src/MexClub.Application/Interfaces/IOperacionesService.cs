using MexClub.Application.Common;
using MexClub.Application.DTOs.Operaciones;

namespace MexClub.Application.Interfaces;

public interface IAportacionService
{
    Task<ServiceResult<PagedResult<AportacionDto>>> GetAllAsync(PaginationParams pagination, long? socioId = null, CancellationToken ct = default);
    Task<ServiceResult<AportacionDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<AportacionDto>> CreateAsync(CreateAportacionRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<decimal>> GetTotalAsync(CancellationToken ct = default);
}

public interface IRetiradaService
{
    Task<ServiceResult<PagedResult<RetiradaDto>>> GetAllAsync(PaginationParams pagination, long? socioId = null, CancellationToken ct = default);
    Task<ServiceResult<RetiradaDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ServiceResult> CreateListAsync(CreateRetiradaListRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(long id, CancellationToken ct = default);
    Task<ServiceResult<decimal>> GetTotalAsync(CancellationToken ct = default);
}

public interface ICuotaService
{
    Task<ServiceResult<PagedResult<CuotaDto>>> GetAllAsync(PaginationParams pagination, CancellationToken ct = default);
    Task<ServiceResult<CuotaDto>> GetBySocioIdAsync(long socioId, CancellationToken ct = default);
    Task<ServiceResult<CuotaDto>> CreateAsync(CreateCuotaRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(long id, CancellationToken ct = default);
}

public interface IAccesoService
{
    Task<ServiceResult<PagedResult<AccesoDto>>> GetAllAsync(PaginationParams pagination, long? socioId = null, CancellationToken ct = default);
    Task<ServiceResult<FicharResponse>> FicharAsync(FicharRequest request, CancellationToken ct = default);
    Task<ServiceResult<string>> GetEstadoAccesoAsync(long socioId, CancellationToken ct = default);
    Task<ServiceResult<AccesoDto?>> GetUltimoAccesoAsync(long socioId, CancellationToken ct = default);
}

public interface IDashboardService
{
    Task<ServiceResult<DashboardDto>> GetDashboardAsync(CancellationToken ct = default);
}
