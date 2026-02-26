using MexClub.Application.Common;
using MexClub.Application.DTOs.Socios;
using MexClub.Domain.Entities;
using MexClub.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MexClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SociosController : ControllerBase
{
    private readonly IRepository<Socio> _socioRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SociosController> _logger;
    private readonly IConfiguration _config;

    public SociosController(
        IRepository<Socio> socioRepo,
        IUnitOfWork unitOfWork,
        ILogger<SociosController> logger,
        IConfiguration config)
    {
        _socioRepo = socioRepo;
        _unitOfWork = unitOfWork;
        _logger = logger;
        _config = config;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<PagedResult<SocioDto>>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] bool? soloActivos = true,
        CancellationToken ct = default)
    {
        var query = _socioRepo.Query()
            .Include(s => s.Detalle)
            .Include(s => s.ReferidoPor)
            .AsQueryable();

        if (soloActivos == true)
            query = query.Where(s => s.IsActive);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(s => s.Nombre).ThenBy(s => s.PrimerApellido).ThenBy(s => s.SegundoApellido)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        var result = new PagedResult<SocioDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return Ok(ServiceResult<PagedResult<SocioDto>>.Ok(result));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ServiceResult<SocioDto>>> GetById(long id, CancellationToken ct)
    {
        var socio = await _socioRepo.Query()
            .Include(s => s.Detalle)
            .Include(s => s.ReferidoPor)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (socio == null)
            return NotFound(ServiceResult.Fail("Socio no encontrado."));

        return Ok(ServiceResult<SocioDto>.Ok(MapToDto(socio)));
    }

    [HttpGet("buscar/{codigoODocumento}")]
    public async Task<ActionResult<ServiceResult<SocioDto>>> GetByCodigoOrDocumento(string codigoODocumento, CancellationToken ct)
    {
        var socio = await _socioRepo.Query()
            .Include(s => s.Detalle)
            .Include(s => s.ReferidoPor)
            .FirstOrDefaultAsync(s => s.Codigo == codigoODocumento || s.Documento == codigoODocumento, ct);

        if (socio == null)
            return NotFound(ServiceResult.Fail("Socio no encontrado."));

        return Ok(ServiceResult<SocioDto>.Ok(MapToDto(socio)));
    }

    [HttpGet("search")]
    public async Task<ActionResult<ServiceResult<IReadOnlyList<SocioDto>>>> Search(
        [FromQuery] string q = "",
        [FromQuery] int limit = 30,
        CancellationToken ct = default)
    {
        var term = (q ?? "").Trim().ToLower();

        var query = _socioRepo.Query()
            .Include(s => s.Detalle)
            .Include(s => s.ReferidoPor)
            .Where(s => s.IsActive);

        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(s =>
                (s.Nombre + " " + s.PrimerApellido + " " + (s.SegundoApellido ?? "")).ToLower().StartsWith(term) ||
                s.Nombre.ToLower().StartsWith(term) ||
                s.PrimerApellido.ToLower().StartsWith(term) ||
                (s.SegundoApellido != null && s.SegundoApellido.ToLower().StartsWith(term)) ||
                s.Codigo.ToLower().StartsWith(term) ||
                s.Documento.ToLower().StartsWith(term));
        }

        var items = await query
            .OrderBy(s => s.Nombre).ThenBy(s => s.PrimerApellido).ThenBy(s => s.SegundoApellido)
            .Take(limit)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        return Ok(ServiceResult<IReadOnlyList<SocioDto>>.Ok(items));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<SocioDto>>> Create([FromBody] CreateSocioRequest request, CancellationToken ct)
    {
        if (await _socioRepo.ExistsAsync(s => s.Documento == request.Documento, ct))
            return Conflict(ServiceResult.Fail("Ya existe un socio con ese documento."));

        var maxNumSocio = await _socioRepo.Query().MaxAsync(s => (int?)s.NumSocio, ct) ?? 0;

        long? referidoPorId = null;
        if (!string.IsNullOrEmpty(request.ReferidoPor))
        {
            var referido = await _socioRepo.FirstOrDefaultAsync(s => s.Codigo == request.ReferidoPor, ct);
            referidoPorId = referido?.Id;
        }

        var socio = new Socio
        {
            NumSocio = maxNumSocio + 1,
            Codigo = request.Codigo,
            ReferidoPorSocioId = referidoPorId,
            Nombre = request.Nombre,
            PrimerApellido = request.PrimerApellido,
            SegundoApellido = request.SegundoApellido,
            TipoDocumento = request.TipoDocumento,
            Documento = request.Documento,
            Pais = request.Pais,
            Provincia = request.Provincia,
            Localidad = request.Localidad,
            Direccion = request.Direccion,
            CodigoPostal = request.CodigoPostal,
            Telefono = request.Telefono,
            Email = request.Email,
            FechaNacimiento = EnsureUtc(request.FechaNacimiento),
            FechaAlta = DateTime.UtcNow,
            Estrellas = request.Estrellas,
            ConsumicionMaximaMensual = request.ConsumicionMaximaMensual,
            EsTerapeutica = request.EsTerapeutica,
            EsExento = request.EsExento,
            PagoConTarjeta = request.PagoConTarjeta,
            Comentario = request.Comentario,
            Detalle = new SocioDetalle()
        };

        await _socioRepo.AddAsync(socio, ct);
        _logger.LogInformation("Socio creado: {NumSocio} - {Nombre}", socio.NumSocio, socio.NombreCompleto);

        return CreatedAtAction(nameof(GetById), new { id = socio.Id },
            ServiceResult<SocioDto>.Ok(MapToDto(socio)));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<SocioDto>>> Update(long id, [FromBody] UpdateSocioRequest request, CancellationToken ct)
    {
        var socio = await _socioRepo.GetByIdAsync(id, ct);
        if (socio == null)
            return NotFound(ServiceResult.Fail("Socio no encontrado."));

        long? referidoPorId = null;
        if (!string.IsNullOrEmpty(request.ReferidoPor))
        {
            var referido = await _socioRepo.FirstOrDefaultAsync(s => s.Codigo == request.ReferidoPor, ct);
            referidoPorId = referido?.Id;
        }

        socio.Codigo = request.Codigo;
        socio.ReferidoPorSocioId = referidoPorId;
        socio.Nombre = request.Nombre;
        socio.PrimerApellido = request.PrimerApellido;
        socio.SegundoApellido = request.SegundoApellido;
        socio.TipoDocumento = request.TipoDocumento;
        socio.Documento = request.Documento;
        socio.Pais = request.Pais;
        socio.Provincia = request.Provincia;
        socio.Localidad = request.Localidad;
        socio.Direccion = request.Direccion;
        socio.CodigoPostal = request.CodigoPostal;
        socio.Telefono = request.Telefono;
        socio.Email = request.Email;
        socio.FechaNacimiento = EnsureUtc(request.FechaNacimiento);
        socio.Estrellas = request.Estrellas;
        socio.ConsumicionMaximaMensual = request.ConsumicionMaximaMensual;
        socio.EsTerapeutica = request.EsTerapeutica;
        socio.EsExento = request.EsExento;
        socio.PagoConTarjeta = request.PagoConTarjeta;
        socio.Comentario = request.Comentario;

        await _socioRepo.UpdateAsync(socio, ct);
        return Ok(ServiceResult<SocioDto>.Ok(MapToDto(socio)));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult>> Deactivate(long id, CancellationToken ct)
    {
        var socio = await _socioRepo.GetByIdAsync(id, ct);
        if (socio == null)
            return NotFound(ServiceResult.Fail("Socio no encontrado."));

        socio.IsActive = false;
        await _socioRepo.UpdateAsync(socio, ct);

        _logger.LogInformation("Socio desactivado: {Id}", id);
        return Ok(ServiceResult.Ok("Socio desactivado."));
    }

    [HttpGet("referidos/{codigo}")]
    public async Task<ActionResult<ServiceResult<IReadOnlyList<SocioDto>>>> GetReferidos(string codigo, CancellationToken ct)
    {
        var socio = await _socioRepo.FirstOrDefaultAsync(s => s.Codigo == codigo, ct);
        if (socio == null)
            return NotFound(ServiceResult.Fail("Socio no encontrado."));

        var referidos = await _socioRepo.Query()
            .Where(s => s.ReferidoPorSocioId == socio.Id && s.IsActive)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        return Ok(ServiceResult<IReadOnlyList<SocioDto>>.Ok(referidos));
    }

    [HttpPost("{id:long}/upload")]
    [Authorize(Policy = "Admin")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<ServiceResult<SocioDto>>> Upload(
        long id,
        [FromForm] IFormFile? foto,
        [FromForm] IFormFile? dniAnverso,
        [FromForm] IFormFile? dniReverso,
        CancellationToken ct)
    {
        var socio = await _socioRepo.Query()
            .Include(s => s.Detalle).Include(s => s.ReferidoPor)
            .FirstOrDefaultAsync(s => s.Id == id, ct);
        if (socio == null)
            return NotFound(ServiceResult.Fail("Socio no encontrado."));

        var basePath = _config["FileStorage:BasePath"] ?? "resources";
        var requestPath = _config["FileStorage:RequestPath"] ?? "/resources";
        var resourcesRoot = Path.IsPathRooted(basePath)
            ? basePath
            : Path.Combine(Directory.GetCurrentDirectory(), basePath);

        async Task<string> SaveFile(IFormFile file, string subfolder, string prefix)
        {
            var dir = Path.Combine(resourcesRoot, subfolder);
            Directory.CreateDirectory(dir);
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(ext)) ext = ".jpg";
            var fileName = prefix + "_" + id + "_" + DateTime.UtcNow.Ticks + ext;
            var filePath = Path.Combine(dir, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream, ct);
            return $"{requestPath}/{subfolder}/{fileName}";
        }

        if (foto != null) socio.FotoUrl = await SaveFile(foto, "fotos", "foto");
        if (dniAnverso != null) socio.FotoAnversoDniUrl = await SaveFile(dniAnverso, "documentos", "dni_anverso");
        if (dniReverso != null) socio.FotoReversoDniUrl = await SaveFile(dniReverso, "documentos", "dni_reverso");

        await _socioRepo.UpdateAsync(socio, ct);
        _logger.LogInformation("Imágenes subidas para socio: {Id}", id);

        return Ok(ServiceResult<SocioDto>.Ok(MapToDto(socio)));
    }

    private static SocioDto MapToDto(Socio s) => new(
        s.Id, s.NumSocio, s.Codigo, s.ReferidoPorSocioId,
        s.ReferidoPor?.Codigo,
        s.Nombre, s.PrimerApellido, s.SegundoApellido, s.NombreCompleto,
        s.TipoDocumento, s.Documento, s.Pais, s.Provincia, s.Localidad,
        s.Direccion, s.CodigoPostal, s.Telefono, s.Email, s.FechaNacimiento,
        s.FechaAlta, s.FotoUrl, s.FotoAnversoDniUrl, s.FotoReversoDniUrl,
        s.IsActive, s.Estrellas,
        s.ConsumicionMaximaMensual, s.EsTerapeutica, s.EsExento, s.PagoConTarjeta,
        s.Comentario,
        s.Detalle != null ? new SocioDetalleDto(
            s.Detalle.CuotaFechaProxima, s.Detalle.ConsumicionDelMes,
            s.Detalle.FechaUltimaConsumicion, s.Detalle.FechaUltimaAportacion,
            s.Detalle.ExentoCuota, s.Detalle.DebeCuota, s.Detalle.Aprovechable
        ) : null
    );

    private static DateTime? EnsureUtc(DateTime? dt)
    {
        if (!dt.HasValue) return null;
        return DateTime.SpecifyKind(dt.Value, DateTimeKind.Utc);
    }
}
