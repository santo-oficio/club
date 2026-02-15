using MexClub.Application.Common;
using MexClub.Application.DTOs.Operaciones;
using MexClub.Domain.Entities;
using MexClub.Domain.Interfaces;
using MexClub.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MexClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AportacionesController : ControllerBase
{
    private readonly MexClubDbContext _db;
    private readonly IRepository<Aportacion> _aportacionRepo;

    public AportacionesController(MexClubDbContext db, IRepository<Aportacion> aportacionRepo)
    {
        _db = db;
        _aportacionRepo = aportacionRepo;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<PagedResult<AportacionDto>>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] long? socioId = null, CancellationToken ct = default)
    {
        var query = _aportacionRepo.Query()
            .Include(a => a.Socio).Include(a => a.Usuario).AsQueryable();
        if (socioId.HasValue) query = query.Where(a => a.SocioId == socioId.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.Fecha)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AportacionDto(a.Id, a.SocioId, a.Socio.NombreCompleto, a.Socio.NumSocio, a.Socio.Documento, a.Socio.FotoUrl, a.UsuarioId, a.CantidadAportada, a.Fecha, a.Codigo))
            .ToListAsync(ct);

        return Ok(ServiceResult<PagedResult<AportacionDto>>.Ok(new PagedResult<AportacionDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize }));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<AportacionDto>>> Create([FromBody] CreateAportacionRequest request, CancellationToken ct)
    {
        // Tracked query — necesario para que EF persista los cambios en Detalle
        var socio = await _db.Socios.Include(s => s.Detalle).FirstOrDefaultAsync(s => s.Id == request.SocioId, ct);
        if (socio == null) return NotFound(ServiceResult.Fail("Socio no encontrado."));

        var aportacion = new Aportacion
        {
            SocioId = request.SocioId,
            UsuarioId = request.UsuarioId,
            CantidadAportada = request.CantidadAportada,
            Fecha = DateTime.UtcNow,
            Codigo = Guid.NewGuid().ToString()[..5]
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Aportaciones.Add(aportacion);

            if (socio.Detalle != null)
            {
                socio.Detalle.Aprovechable += request.CantidadAportada;
                socio.Detalle.FechaUltimaAportacion = DateTime.UtcNow;
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return CreatedAtAction(nameof(GetAll), null,
            ServiceResult<AportacionDto>.Ok(new AportacionDto(aportacion.Id, aportacion.SocioId, socio.NombreCompleto, socio.NumSocio, socio.Documento, socio.FotoUrl, aportacion.UsuarioId, aportacion.CantidadAportada, aportacion.Fecha, aportacion.Codigo)));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult>> Delete(long id, CancellationToken ct)
    {
        var aportacion = await _db.Aportaciones.FirstOrDefaultAsync(a => a.Id == id, ct);
        if (aportacion == null) return NotFound(ServiceResult.Fail("Aportación no encontrada."));

        var socio = await _db.Socios.Include(s => s.Detalle).FirstOrDefaultAsync(s => s.Id == aportacion.SocioId, ct);

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            if (socio?.Detalle != null)
            {
                socio.Detalle.Aprovechable -= aportacion.CantidadAportada;
            }
            _db.Aportaciones.Remove(aportacion);
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return Ok(ServiceResult.Ok("Aportación eliminada."));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccesosController : ControllerBase
{
    private readonly IRepository<Acceso> _accesoRepo;
    private readonly IRepository<Socio> _socioRepo;
    private readonly ILogger<AccesosController> _logger;

    public AccesosController(IRepository<Acceso> accesoRepo, IRepository<Socio> socioRepo, ILogger<AccesosController> logger)
    {
        _accesoRepo = accesoRepo;
        _socioRepo = socioRepo;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<PagedResult<AccesoDto>>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] long? socioId = null, CancellationToken ct = default)
    {
        var query = _accesoRepo.Query().Include(a => a.Socio).AsQueryable();
        if (socioId.HasValue) query = query.Where(a => a.SocioId == socioId.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(a => a.FechaHora)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new AccesoDto(a.Id, a.SocioId, a.Socio.NombreCompleto, a.TipoAcceso.ToString(), a.FechaHora, a.Turno, a.Accion.ToString()))
            .ToListAsync(ct);

        return Ok(ServiceResult<PagedResult<AccesoDto>>.Ok(new PagedResult<AccesoDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize }));
    }

    [HttpPost("fichar")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<FicharResponse>>> Fichar([FromBody] FicharRequest request, CancellationToken ct)
    {
        var socio = await _socioRepo.GetByIdAsync(request.SocioId, ct);
        if (socio == null) return NotFound(ServiceResult.Fail("Socio no encontrado."));

        var hoy = DateTime.UtcNow.Date;
        var ultimoAcceso = await _accesoRepo.Query()
            .Where(a => a.SocioId == request.SocioId && a.FechaHora >= hoy)
            .OrderByDescending(a => a.FechaHora)
            .FirstOrDefaultAsync(ct);

        var tipoAcceso = ultimoAcceso == null || ultimoAcceso.TipoAcceso == TipoAcceso.Salida
            ? TipoAcceso.Entrada
            : TipoAcceso.Salida;

        var acceso = new Acceso
        {
            SocioId = request.SocioId,
            TipoAcceso = tipoAcceso,
            FechaHora = DateTime.UtcNow,
            Accion = AccionAcceso.Ok
        };

        await _accesoRepo.AddAsync(acceso, ct);
        _logger.LogInformation("Fichaje: Socio {SocioId} - {Tipo}", request.SocioId, tipoAcceso);

        return Ok(ServiceResult<FicharResponse>.Ok(new FicharResponse(tipoAcceso.ToString(), acceso.FechaHora)));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CuotasController : ControllerBase
{
    private readonly MexClubDbContext _db;
    private readonly IRepository<Cuota> _cuotaRepo;
    private readonly IRepository<Aportacion> _aportacionRepo;

    public CuotasController(MexClubDbContext db, IRepository<Cuota> cuotaRepo,
        IRepository<Aportacion> aportacionRepo)
    {
        _db = db;
        _cuotaRepo = cuotaRepo;
        _aportacionRepo = aportacionRepo;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<PagedResult<CuotaDto>>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = _cuotaRepo.Query().Include(c => c.Socio).Include(c => c.Usuario);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(c => c.Fecha)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(c => new CuotaDto(c.Id, c.SocioId, c.Socio.NombreCompleto, c.Fecha, c.CantidadCuota, c.Periodo.ToString(), c.UsuarioId, c.FechaAnterior))
            .ToListAsync(ct);

        return Ok(ServiceResult<PagedResult<CuotaDto>>.Ok(new PagedResult<CuotaDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize }));
    }

    /// <summary>Última cuota de un socio (la más reciente por fecha).</summary>
    [HttpGet("ultima-por-socio/{socioId:long}")]
    public async Task<ActionResult<ServiceResult<CuotaDto?>>> GetUltimaPorSocio(long socioId, CancellationToken ct)
    {
        var cuota = await _cuotaRepo.Query()
            .Include(c => c.Socio)
            .Where(c => c.SocioId == socioId)
            .OrderByDescending(c => c.Fecha)
            .FirstOrDefaultAsync(ct);

        if (cuota == null)
            return Ok(ServiceResult<CuotaDto?>.Ok(null));

        return Ok(ServiceResult<CuotaDto?>.Ok(
            new CuotaDto(cuota.Id, cuota.SocioId, cuota.Socio.NombreCompleto, cuota.Fecha,
                cuota.CantidadCuota, cuota.Periodo.ToString(), cuota.UsuarioId, cuota.FechaAnterior)));
    }

    /// <summary>Devuelve el socioId de la última aportación registrada.</summary>
    [HttpGet("socio-ultima-aportacion")]
    public async Task<ActionResult<ServiceResult<long?>>> GetSocioUltimaAportacion(CancellationToken ct)
    {
        var aportacion = await _aportacionRepo.Query()
            .OrderByDescending(a => a.Id)
            .Select(a => new { a.SocioId })
            .FirstOrDefaultAsync(ct);

        return Ok(ServiceResult<long?>.Ok(aportacion?.SocioId));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<CuotaDto>>> Create([FromBody] CreateCuotaRequest request, CancellationToken ct)
    {
        // Tracked query — necesario para que EF persista los cambios en Detalle
        var socio = await _db.Socios.Include(s => s.Detalle).FirstOrDefaultAsync(s => s.Id == request.SocioId, ct);
        if (socio == null) return NotFound(ServiceResult.Fail("Socio no encontrado."));

        var periodo = (PeriodoCuota)request.Periodo;
        var raw = socio.Detalle?.CuotaFechaProxima ?? DateTime.UtcNow;
        var fechaAnterior = raw.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(raw, DateTimeKind.Utc) : raw.ToUniversalTime();

        // Calco exacto del original: fechaProxima se calcula desde DateTime.Now, no desde fechaAnterior
        var fechaProxima = periodo == PeriodoCuota.Anual
            ? DateTime.UtcNow.AddYears(1)
            : DateTime.UtcNow.AddMonths(1);

        var cuota = new Cuota
        {
            SocioId = request.SocioId,
            Fecha = DateTime.UtcNow,
            CantidadCuota = request.CantidadCuota,
            Periodo = periodo,
            UsuarioId = request.UsuarioId,
            FechaAnterior = fechaAnterior
        };

        await using var tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            _db.Cuotas.Add(cuota);
            if (socio.Detalle != null)
            {
                socio.Detalle.CuotaFechaProxima = fechaProxima;
                socio.Detalle.DebeCuota = false;
            }
            await _db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }

        return CreatedAtAction(nameof(GetAll), null,
            ServiceResult<CuotaDto>.Ok(new CuotaDto(cuota.Id, cuota.SocioId, socio.NombreCompleto, cuota.Fecha, cuota.CantidadCuota, cuota.Periodo.ToString(), cuota.UsuarioId, cuota.FechaAnterior)));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class RetiradasController : ControllerBase
{
    private readonly IRepository<Retirada> _retiradaRepo;
    private readonly IRepository<Socio> _socioRepo;
    private readonly IRepository<Articulo> _articuloRepo;
    private readonly IUnitOfWork _unitOfWork;

    public RetiradasController(
        IRepository<Retirada> retiradaRepo, IRepository<Socio> socioRepo,
        IRepository<Articulo> articuloRepo, IUnitOfWork unitOfWork)
    {
        _retiradaRepo = retiradaRepo;
        _socioRepo = socioRepo;
        _articuloRepo = articuloRepo;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<PagedResult<RetiradaDto>>>> GetAll(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] long? socioId = null, CancellationToken ct = default)
    {
        var query = _retiradaRepo.Query()
            .Include(r => r.Socio).Include(r => r.Articulo).Include(r => r.Usuario).AsQueryable();
        if (socioId.HasValue) query = query.Where(r => r.SocioId == socioId.Value);

        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(r => r.Fecha)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new RetiradaDto(r.Id, r.SocioId, r.Socio.NombreCompleto, r.Socio.NumSocio, r.Socio.Documento, r.Socio.FotoUrl,
                r.ArticuloId, r.Articulo.Nombre, r.UsuarioId, r.PrecioArticulo, r.Cantidad, r.Total, r.FirmaUrl, r.Fecha))
            .ToListAsync(ct);

        return Ok(ServiceResult<PagedResult<RetiradaDto>>.Ok(new PagedResult<RetiradaDto> { Items = items, TotalCount = total, Page = page, PageSize = pageSize }));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<RetiradaDto>>> Create([FromBody] CreateRetiradaRequest request, CancellationToken ct)
    {
        var socio = await _socioRepo.Query().Include(s => s.Detalle).FirstOrDefaultAsync(s => s.Id == request.SocioId, ct);
        if (socio == null) return NotFound(ServiceResult.Fail("Socio no encontrado."));

        var articulo = await _articuloRepo.GetByIdAsync(request.ArticuloId, ct);
        if (articulo == null) return NotFound(ServiceResult.Fail("Artículo no encontrado."));

        var total = articulo.Precio * request.Cantidad;

        if (socio.Detalle != null && socio.Detalle.Aprovechable < total)
            return BadRequest(ServiceResult.Fail($"Saldo insuficiente. Disponible: {socio.Detalle.Aprovechable:F2} €, Necesario: {total:F2} €"));

        var retirada = new Retirada
        {
            SocioId = request.SocioId,
            ArticuloId = request.ArticuloId,
            UsuarioId = request.UsuarioId,
            PrecioArticulo = articulo.Precio,
            Cantidad = request.Cantidad,
            Total = total,
            FirmaUrl = request.FirmaBase64,
            Fecha = DateTime.UtcNow
        };

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            await _retiradaRepo.AddAsync(retirada, ct);
            if (socio.Detalle != null)
            {
                socio.Detalle.Aprovechable -= total;
                socio.Detalle.ConsumicionDelMes += total;
                socio.Detalle.FechaUltimaConsumicion = DateTime.UtcNow;
            }
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }

        return CreatedAtAction(nameof(GetAll), null,
            ServiceResult<RetiradaDto>.Ok(new RetiradaDto(retirada.Id, retirada.SocioId, socio.NombreCompleto, socio.NumSocio, socio.Documento, socio.FotoUrl,
                retirada.ArticuloId, articulo.Nombre, retirada.UsuarioId, retirada.PrecioArticulo, retirada.Cantidad, retirada.Total, retirada.FirmaUrl, retirada.Fecha)));
    }

    [HttpPost("batch")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<List<RetiradaDto>>>> CreateBatch([FromBody] CreateRetiradaListRequest request, CancellationToken ct)
    {
        var socio = await _socioRepo.Query().Include(s => s.Detalle).FirstOrDefaultAsync(s => s.Id == request.SocioId, ct);
        if (socio == null) return NotFound(ServiceResult.Fail("Socio no encontrado."));

        var articuloIds = request.Items.Select(i => i.ArticuloId).Distinct().ToList();
        var articulos = await _articuloRepo.Query().Where(a => articuloIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, ct);

        var retiradas = new List<Retirada>();
        decimal totalGeneral = 0;

        foreach (var item in request.Items)
        {
            if (!articulos.TryGetValue(item.ArticuloId, out var art))
                return NotFound(ServiceResult.Fail($"Artículo {item.ArticuloId} no encontrado."));

            var total = art.Precio * item.Cantidad;
            totalGeneral += total;
            retiradas.Add(new Retirada
            {
                SocioId = request.SocioId,
                ArticuloId = item.ArticuloId,
                UsuarioId = request.UsuarioId,
                PrecioArticulo = art.Precio,
                Cantidad = item.Cantidad,
                Total = total,
                FirmaUrl = request.FirmaBase64,
                Fecha = DateTime.UtcNow
            });
        }

        if (socio.Detalle != null && socio.Detalle.Aprovechable < totalGeneral)
            return BadRequest(ServiceResult.Fail($"Saldo insuficiente. Disponible: {socio.Detalle.Aprovechable:F2} €, Necesario: {totalGeneral:F2} €"));

        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            foreach (var r in retiradas) await _retiradaRepo.AddAsync(r, ct);
            if (socio.Detalle != null)
            {
                socio.Detalle.Aprovechable -= totalGeneral;
                socio.Detalle.ConsumicionDelMes += totalGeneral;
                socio.Detalle.FechaUltimaConsumicion = DateTime.UtcNow;
            }
            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }

        var dtos = retiradas.Select(r => new RetiradaDto(r.Id, r.SocioId, socio.NombreCompleto, socio.NumSocio, socio.Documento, socio.FotoUrl,
            r.ArticuloId, articulos[r.ArticuloId].Nombre, r.UsuarioId, r.PrecioArticulo, r.Cantidad, r.Total, r.FirmaUrl, r.Fecha)).ToList();

        return Ok(ServiceResult<List<RetiradaDto>>.Ok(dtos));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class DashboardController : ControllerBase
{
    private readonly IRepository<Socio> _socioRepo;
    private readonly IRepository<Acceso> _accesoRepo;
    private readonly IRepository<Aportacion> _aportacionRepo;
    private readonly IRepository<Retirada> _retiradaRepo;

    public DashboardController(
        IRepository<Socio> socioRepo, IRepository<Acceso> accesoRepo,
        IRepository<Aportacion> aportacionRepo, IRepository<Retirada> retiradaRepo)
    {
        _socioRepo = socioRepo;
        _accesoRepo = accesoRepo;
        _aportacionRepo = aportacionRepo;
        _retiradaRepo = retiradaRepo;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<DashboardDto>>> Get(CancellationToken ct)
    {
        var totalSocios = await _socioRepo.CountAsync(s => s.IsActive, ct);

        var hoy = DateTime.UtcNow.Date;
        var accesosHoy = await _accesoRepo.Query()
            .Where(a => a.FechaHora >= hoy).Include(a => a.Socio)
            .OrderByDescending(a => a.FechaHora).Take(20)
            .ToListAsync(ct);

        var entradas = accesosHoy.Count(a => a.TipoAcceso == TipoAcceso.Entrada);
        var salidas = accesosHoy.Count(a => a.TipoAcceso == TipoAcceso.Salida);

        var ultimosAccesos = accesosHoy.Take(10).Select(a =>
            new AccesoDto(a.Id, a.SocioId, a.Socio.NombreCompleto, a.TipoAcceso.ToString(), a.FechaHora, a.Turno, a.Accion.ToString())).ToList();

        var ultimasAportaciones = await _aportacionRepo.Query()
            .Include(a => a.Socio).OrderByDescending(a => a.Fecha).Take(10)
            .Select(a => new AportacionDto(a.Id, a.SocioId, a.Socio.NombreCompleto, a.Socio.NumSocio, a.Socio.Documento, a.Socio.FotoUrl, a.UsuarioId, a.CantidadAportada, a.Fecha, a.Codigo))
            .ToListAsync(ct);

        var ultimasRetiradas = await _retiradaRepo.Query()
            .Include(r => r.Socio).Include(r => r.Articulo).OrderByDescending(r => r.Fecha).Take(10)
            .Select(r => new RetiradaDto(r.Id, r.SocioId, r.Socio.NombreCompleto, r.Socio.NumSocio, r.Socio.Documento, r.Socio.FotoUrl,
                r.ArticuloId, r.Articulo.Nombre, r.UsuarioId, r.PrecioArticulo, r.Cantidad, r.Total, r.FirmaUrl, r.Fecha))
            .ToListAsync(ct);

        var totalAportaciones = await _aportacionRepo.Query().SumAsync(a => a.CantidadAportada, ct);
        var totalRetiradas = await _retiradaRepo.Query().SumAsync(r => r.Total, ct);

        return Ok(ServiceResult<DashboardDto>.Ok(new DashboardDto(
            totalSocios, Math.Max(0, entradas - salidas), totalAportaciones, totalRetiradas,
            ultimosAccesos, ultimasAportaciones, ultimasRetiradas)));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PingController : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public ActionResult<ServiceResult<string>> Ping()
    {
        return Ok(ServiceResult<string>.Ok("OK"));
    }
}
