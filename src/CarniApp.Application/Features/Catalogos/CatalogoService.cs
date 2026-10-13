using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.Catalogos;

/// <summary>
/// RF-INV-001/002, RF-CONFIG-002: administracion del catalogo de
/// cortes y tipos de animal. Valida nombres duplicados, unidad de
/// venta valida y que un tipo de animal con cortes activos no se
/// pueda desactivar por error (romperia el FK de esos cortes al
/// armar el POS).
/// </summary>
public class CatalogoService : ICatalogoService
{
    private readonly ITipoAnimalRepository _tipoAnimalRepository;
    private readonly ICorteRepository _corteRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CatalogoService(
        ITipoAnimalRepository tipoAnimalRepository,
        ICorteRepository corteRepository,
        IUnitOfWork unitOfWork)
    {
        _tipoAnimalRepository = tipoAnimalRepository;
        _corteRepository = corteRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<TipoAnimalDto>> ListarTiposAnimalAsync(CancellationToken cancellationToken = default)
    {
        var tipos = await _tipoAnimalRepository.GetAllAsync(cancellationToken);
        var resultado = new List<TipoAnimalDto>();

        foreach (var tipo in tipos)
        {
            var cantidad = await _tipoAnimalRepository.ContarCortesActivosAsync(tipo.Id, cancellationToken);
            resultado.Add(new TipoAnimalDto(tipo.Id, tipo.Nombre, cantidad));
        }

        return resultado;
    }

    public async Task<CatalogoResultDto<TipoAnimalDto>> CrearTipoAnimalAsync(GuardarTipoAnimalRequest request, CancellationToken cancellationToken = default)
    {
        var nombre = request.Nombre.Trim();

        if (await _tipoAnimalRepository.ExisteNombreAsync(nombre, cancellationToken: cancellationToken))
        {
            return CatalogoResultDto<TipoAnimalDto>.Fail(CatalogoResultCode.NombreDuplicado, $"Ya existe un tipo de animal llamado '{nombre}'.");
        }

        var tipoAnimal = new TipoAnimal { Nombre = nombre };
        await _tipoAnimalRepository.AddAsync(tipoAnimal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CatalogoResultDto<TipoAnimalDto>.Ok(new TipoAnimalDto(tipoAnimal.Id, tipoAnimal.Nombre, 0));
    }

    public async Task<CatalogoResultDto<TipoAnimalDto>> ActualizarTipoAnimalAsync(Guid id, GuardarTipoAnimalRequest request, CancellationToken cancellationToken = default)
    {
        var tipoAnimal = await _tipoAnimalRepository.GetByIdAsync(id, cancellationToken);

        if (tipoAnimal is null)
        {
            return CatalogoResultDto<TipoAnimalDto>.Fail(CatalogoResultCode.NoEncontrado, "Tipo de animal no encontrado.");
        }

        var nombre = request.Nombre.Trim();

        if (await _tipoAnimalRepository.ExisteNombreAsync(nombre, id, cancellationToken))
        {
            return CatalogoResultDto<TipoAnimalDto>.Fail(CatalogoResultCode.NombreDuplicado, $"Ya existe un tipo de animal llamado '{nombre}'.");
        }

        tipoAnimal.Nombre = nombre;
        _tipoAnimalRepository.Update(tipoAnimal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var cantidad = await _tipoAnimalRepository.ContarCortesActivosAsync(tipoAnimal.Id, cancellationToken);
        return CatalogoResultDto<TipoAnimalDto>.Ok(new TipoAnimalDto(tipoAnimal.Id, tipoAnimal.Nombre, cantidad));
    }

    public async Task<CatalogoResultDto<bool>> DesactivarTipoAnimalAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var tipoAnimal = await _tipoAnimalRepository.GetByIdAsync(id, cancellationToken);

        if (tipoAnimal is null)
        {
            return CatalogoResultDto<bool>.Fail(CatalogoResultCode.NoEncontrado, "Tipo de animal no encontrado.");
        }

        var cortesActivos = await _tipoAnimalRepository.ContarCortesActivosAsync(id, cancellationToken);

        if (cortesActivos > 0)
        {
            return CatalogoResultDto<bool>.Fail(
                CatalogoResultCode.TipoAnimalConCortesActivos,
                $"No se puede desactivar: tiene {cortesActivos} corte(s) activo(s). Desactiva primero esos cortes.");
        }

        tipoAnimal.Activo = false;
        _tipoAnimalRepository.Update(tipoAnimal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CatalogoResultDto<bool>.Ok(true);
    }

    public async Task<IReadOnlyList<CorteDto>> ListarCortesAsync(CancellationToken cancellationToken = default)
    {
        var cortes = await _corteRepository.GetAllConTipoAnimalAsync(cancellationToken);
        return cortes.Select(Mapear).ToList();
    }

    public async Task<CatalogoResultDto<CorteDto>> CrearCorteAsync(GuardarCorteRequest request, CancellationToken cancellationToken = default)
    {
        var validacion = await ValidarCorteAsync(request, null, cancellationToken);
        if (validacion is not null) return validacion;

        var corte = new Corte
        {
            TipoAnimalId = request.TipoAnimalId,
            Nombre = request.Nombre.Trim(),
            UnidadVenta = Enum.Parse<UnidadVenta>(request.UnidadVenta, ignoreCase: true),
            PrecioBase = request.PrecioBase,
            StockMinimo = request.StockMinimo
        };

        await _corteRepository.AddAsync(corte, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var creado = await _corteRepository.GetByIdConTipoAnimalAsync(corte.Id, cancellationToken);
        return CatalogoResultDto<CorteDto>.Ok(Mapear(creado!));
    }

    public async Task<CatalogoResultDto<CorteDto>> ActualizarCorteAsync(Guid id, GuardarCorteRequest request, CancellationToken cancellationToken = default)
    {
        var corte = await _corteRepository.GetByIdConTipoAnimalAsync(id, cancellationToken);

        if (corte is null)
        {
            return CatalogoResultDto<CorteDto>.Fail(CatalogoResultCode.NoEncontrado, "Corte no encontrado.");
        }

        var validacion = await ValidarCorteAsync(request, id, cancellationToken);
        if (validacion is not null) return validacion;

        corte.TipoAnimalId = request.TipoAnimalId;
        corte.Nombre = request.Nombre.Trim();
        corte.UnidadVenta = Enum.Parse<UnidadVenta>(request.UnidadVenta, ignoreCase: true);
        corte.PrecioBase = request.PrecioBase;
        corte.StockMinimo = request.StockMinimo;

        _corteRepository.Update(corte);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var actualizado = await _corteRepository.GetByIdConTipoAnimalAsync(corte.Id, cancellationToken);
        return CatalogoResultDto<CorteDto>.Ok(Mapear(actualizado!));
    }

    public async Task<CatalogoResultDto<bool>> DesactivarCorteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var corte = await _corteRepository.GetByIdAsync(id, cancellationToken);

        if (corte is null)
        {
            return CatalogoResultDto<bool>.Fail(CatalogoResultCode.NoEncontrado, "Corte no encontrado.");
        }

        corte.Activo = false;
        _corteRepository.Update(corte);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CatalogoResultDto<bool>.Ok(true);
    }

    private async Task<CatalogoResultDto<CorteDto>?> ValidarCorteAsync(GuardarCorteRequest request, Guid? excluirId, CancellationToken cancellationToken)
    {
        if (request.PrecioBase <= 0)
        {
            return CatalogoResultDto<CorteDto>.Fail(CatalogoResultCode.PrecioInvalido, "El precio base debe ser mayor a cero.");
        }

        if (!Enum.TryParse<UnidadVenta>(request.UnidadVenta, ignoreCase: true, out _))
        {
            return CatalogoResultDto<CorteDto>.Fail(CatalogoResultCode.UnidadVentaInvalida, "La unidad de venta debe ser 'Peso' o 'Pieza'.");
        }

        var tipoAnimal = await _tipoAnimalRepository.GetByIdAsync(request.TipoAnimalId, cancellationToken);
        if (tipoAnimal is null)
        {
            return CatalogoResultDto<CorteDto>.Fail(CatalogoResultCode.TipoAnimalNoEncontrado, "El tipo de animal indicado no existe.");
        }

        var nombre = request.Nombre.Trim();
        if (await _corteRepository.ExisteNombreAsync(nombre, excluirId, cancellationToken))
        {
            return CatalogoResultDto<CorteDto>.Fail(CatalogoResultCode.NombreDuplicado, $"Ya existe un corte llamado '{nombre}'.");
        }

        return null;
    }

    private static CorteDto Mapear(Corte corte) => new(
        corte.Id,
        corte.TipoAnimalId,
        corte.TipoAnimal.Nombre,
        corte.Nombre,
        corte.UnidadVenta.ToString(),
        corte.PrecioBase,
        corte.StockMinimo);
}
