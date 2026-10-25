using CarniApp.Application.Common.Interfaces;
using CarniApp.Domain.Entities;

namespace CarniApp.Application.Features.Negocio;

/// <summary>
/// RF-CONFIG-003: lectura/edicion de la configuracion de marca del
/// negocio. Cada instalacion tiene exactamente un registro (D-01).
/// </summary>
public class NegocioService : INegocioService
{
    private readonly IRepository<Domain.Entities.Negocio> _negocioRepository;
    private readonly IUnitOfWork _unitOfWork;

    public NegocioService(IRepository<Domain.Entities.Negocio> negocioRepository, IUnitOfWork unitOfWork)
    {
        _negocioRepository = negocioRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<NegocioDto?> ObtenerAsync(CancellationToken cancellationToken = default)
    {
        var negocio = (await _negocioRepository.GetAllAsync(cancellationToken)).FirstOrDefault();
        return negocio is null ? null : Mapear(negocio);
    }

    public async Task<NegocioDto> ActualizarAsync(ActualizarNegocioRequest request, CancellationToken cancellationToken = default)
    {
        var negocio = (await _negocioRepository.GetAllAsync(cancellationToken)).FirstOrDefault();

        if (negocio is null)
        {
            negocio = new Domain.Entities.Negocio();
            await _negocioRepository.AddAsync(negocio, cancellationToken);
        }

        negocio.NombreComercial = request.NombreComercial;
        negocio.LogoUrl = request.LogoUrl;
        negocio.Telefono = request.Telefono;
        negocio.RedSocialPrincipal = request.RedSocialPrincipal;
        negocio.Direccion = request.Direccion;
        negocio.Moneda = request.Moneda;
        negocio.ZonaHoraria = request.ZonaHoraria;
        negocio.ColorPrimario = string.IsNullOrWhiteSpace(request.ColorPrimario) ? null : request.ColorPrimario.Trim();
        negocio.PieTicket = string.IsNullOrWhiteSpace(request.PieTicket) ? null : request.PieTicket.Trim();

        _negocioRepository.Update(negocio);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Mapear(negocio);
    }

    private static NegocioDto Mapear(Domain.Entities.Negocio negocio) => new(
        negocio.Id,
        negocio.NombreComercial,
        negocio.LogoUrl,
        negocio.Telefono,
        negocio.RedSocialPrincipal,
        negocio.Direccion,
        negocio.Moneda,
        negocio.ZonaHoraria,
        negocio.ColorPrimario,
        negocio.PieTicket);
}
