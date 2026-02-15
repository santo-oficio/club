using MexClub.Application.Common;
using MexClub.Application.DTOs.Productos;
using MexClub.Domain.Entities;
using MexClub.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MexClub.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FamiliasController : ControllerBase
{
    private readonly IRepository<Familia> _familiaRepo;
    private readonly IRepository<Articulo> _articuloRepo;

    public FamiliasController(IRepository<Familia> familiaRepo, IRepository<Articulo> articuloRepo)
    {
        _familiaRepo = familiaRepo;
        _articuloRepo = articuloRepo;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<IReadOnlyList<FamiliaDto>>>> GetAll(
        [FromQuery] bool? soloActivas = true, CancellationToken ct = default)
    {
        var query = _familiaRepo.Query();
        if (soloActivas == true) query = query.Where(f => f.IsActive);

        var items = await query.OrderBy(f => f.Nombre)
            .Select(f => new FamiliaDto(f.Id, f.Nombre, f.IsActive, f.Descuento))
            .ToListAsync(ct);

        return Ok(ServiceResult<IReadOnlyList<FamiliaDto>>.Ok(items));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<FamiliaDto>>> Create([FromBody] CreateFamiliaRequest request, CancellationToken ct)
    {
        if (await _familiaRepo.ExistsAsync(f => f.Nombre == request.Nombre && f.IsActive, ct))
            return Conflict(ServiceResult.Fail("Ya existe una familia con ese nombre."));

        var familia = new Familia { Nombre = request.Nombre, Descuento = request.Descuento };
        await _familiaRepo.AddAsync(familia, ct);

        return CreatedAtAction(nameof(GetAll), null,
            ServiceResult<FamiliaDto>.Ok(new FamiliaDto(familia.Id, familia.Nombre, familia.IsActive, familia.Descuento)));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<FamiliaDto>>> Update(long id, [FromBody] UpdateFamiliaRequest request, CancellationToken ct)
    {
        var familia = await _familiaRepo.GetByIdAsync(id, ct);
        if (familia == null) return NotFound(ServiceResult.Fail("Familia no encontrada."));

        familia.Nombre = request.Nombre;
        familia.Descuento = request.Descuento;

        if (request.IsActive.HasValue)
        {
            var wasActive = familia.IsActive;
            familia.IsActive = request.IsActive.Value;

            // Cascade deactivate articles when familia is deactivated
            if (wasActive && !familia.IsActive)
            {
                var articulos = await _articuloRepo.Query()
                    .Where(a => a.FamiliaId == id && a.IsActive)
                    .ToListAsync(ct);
                foreach (var art in articulos)
                {
                    art.IsActive = false;
                    await _articuloRepo.UpdateAsync(art, ct);
                }
            }
        }

        await _familiaRepo.UpdateAsync(familia, ct);

        return Ok(ServiceResult<FamiliaDto>.Ok(new FamiliaDto(familia.Id, familia.Nombre, familia.IsActive, familia.Descuento)));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ServiceResult<FamiliaDto>>> GetById(long id, CancellationToken ct)
    {
        var f = await _familiaRepo.GetByIdAsync(id, ct);
        if (f == null) return NotFound(ServiceResult.Fail("Familia no encontrada."));
        return Ok(ServiceResult<FamiliaDto>.Ok(new FamiliaDto(f.Id, f.Nombre, f.IsActive, f.Descuento)));
    }

    [HttpGet("{id:long}/articulos-activos")]
    public async Task<ActionResult<ServiceResult<int>>> CountActiveArticulos(long id, CancellationToken ct)
    {
        var count = await _articuloRepo.Query()
            .Where(a => a.FamiliaId == id && a.IsActive)
            .CountAsync(ct);
        return Ok(ServiceResult<int>.Ok(count));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult>> Deactivate(long id, CancellationToken ct)
    {
        var familia = await _familiaRepo.GetByIdAsync(id, ct);
        if (familia == null) return NotFound(ServiceResult.Fail("Familia no encontrada."));

        familia.IsActive = false;
        await _familiaRepo.UpdateAsync(familia, ct);

        // Cascade: deactivate all articles of this familia
        var articulos = await _articuloRepo.Query()
            .Where(a => a.FamiliaId == id && a.IsActive)
            .ToListAsync(ct);
        foreach (var art in articulos)
        {
            art.IsActive = false;
            await _articuloRepo.UpdateAsync(art, ct);
        }

        return Ok(ServiceResult.Ok("Familia y artículos asociados desactivados."));
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ArticulosController : ControllerBase
{
    private readonly IRepository<Articulo> _articuloRepo;

    public ArticulosController(IRepository<Articulo> articuloRepo)
    {
        _articuloRepo = articuloRepo;
    }

    [HttpGet]
    public async Task<ActionResult<ServiceResult<IReadOnlyList<ArticuloDto>>>> GetAll(
        [FromQuery] bool? soloActivos = true,
        [FromQuery] long? familiaId = null,
        CancellationToken ct = default)
    {
        var query = _articuloRepo.Query().Include(a => a.Familia).AsQueryable();
        if (soloActivos == true) query = query.Where(a => a.IsActive);
        if (familiaId.HasValue) query = query.Where(a => a.FamiliaId == familiaId.Value);

        var items = await query.OrderBy(a => a.Nombre)
            .Select(a => new ArticuloDto(a.Id, a.FamiliaId, a.Familia.Nombre, a.Nombre, a.Descripcion,
                a.Precio, a.Cantidad1, a.Cantidad2, a.Cantidad3, a.Cantidad4, a.EsDecimal, a.IsActive))
            .ToListAsync(ct);

        return Ok(ServiceResult<IReadOnlyList<ArticuloDto>>.Ok(items));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ServiceResult<ArticuloDto>>> GetById(long id, CancellationToken ct)
    {
        var a = await _articuloRepo.Query().Include(x => x.Familia).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a == null) return NotFound(ServiceResult.Fail("Artículo no encontrado."));

        return Ok(ServiceResult<ArticuloDto>.Ok(new ArticuloDto(a.Id, a.FamiliaId, a.Familia.Nombre, a.Nombre,
            a.Descripcion, a.Precio, a.Cantidad1, a.Cantidad2, a.Cantidad3, a.Cantidad4, a.EsDecimal, a.IsActive)));
    }

    [HttpPost]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<ArticuloDto>>> Create([FromBody] CreateArticuloRequest request, CancellationToken ct)
    {
        if (await _articuloRepo.ExistsAsync(a => a.Nombre == request.Nombre && a.IsActive, ct))
            return Conflict(ServiceResult.Fail("Ya existe un artículo con ese nombre."));

        var articulo = new Articulo
        {
            FamiliaId = request.FamiliaId, Nombre = request.Nombre, Descripcion = request.Descripcion,
            Precio = request.Precio, Cantidad1 = request.Cantidad1, Cantidad2 = request.Cantidad2,
            Cantidad3 = request.Cantidad3, Cantidad4 = request.Cantidad4, EsDecimal = request.EsDecimal
        };
        await _articuloRepo.AddAsync(articulo, ct);

        return CreatedAtAction(nameof(GetById), new { id = articulo.Id },
            ServiceResult<ArticuloDto>.Ok(new ArticuloDto(articulo.Id, articulo.FamiliaId, "", articulo.Nombre,
                articulo.Descripcion, articulo.Precio, articulo.Cantidad1, articulo.Cantidad2,
                articulo.Cantidad3, articulo.Cantidad4, articulo.EsDecimal, articulo.IsActive)));
    }

    [HttpPut("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult<ArticuloDto>>> Update(long id, [FromBody] UpdateArticuloRequest request, CancellationToken ct)
    {
        var articulo = await _articuloRepo.GetByIdAsync(id, ct);
        if (articulo == null) return NotFound(ServiceResult.Fail("Artículo no encontrado."));

        articulo.FamiliaId = request.FamiliaId;
        articulo.Nombre = request.Nombre;
        articulo.Descripcion = request.Descripcion;
        articulo.Precio = request.Precio;
        articulo.Cantidad1 = request.Cantidad1;
        articulo.Cantidad2 = request.Cantidad2;
        articulo.Cantidad3 = request.Cantidad3;
        articulo.Cantidad4 = request.Cantidad4;
        articulo.EsDecimal = request.EsDecimal;
        if (request.IsActive.HasValue)
            articulo.IsActive = request.IsActive.Value;
        await _articuloRepo.UpdateAsync(articulo, ct);

        return Ok(ServiceResult<ArticuloDto>.Ok(new ArticuloDto(articulo.Id, articulo.FamiliaId, "", articulo.Nombre,
            articulo.Descripcion, articulo.Precio, articulo.Cantidad1, articulo.Cantidad2,
            articulo.Cantidad3, articulo.Cantidad4, articulo.EsDecimal, articulo.IsActive)));
    }

    [HttpDelete("{id:long}")]
    [Authorize(Policy = "Admin")]
    public async Task<ActionResult<ServiceResult>> Deactivate(long id, CancellationToken ct)
    {
        var articulo = await _articuloRepo.GetByIdAsync(id, ct);
        if (articulo == null) return NotFound(ServiceResult.Fail("Artículo no encontrado."));

        articulo.IsActive = false;
        await _articuloRepo.UpdateAsync(articulo, ct);
        return Ok(ServiceResult.Ok("Artículo desactivado."));
    }
}
