using FIAP.CatalogAPI.Application.DTOs;

namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IPurchaseService
{
    Task<PurchaseResponseDto> InitiatePurchaseAsync(PurchaseRequestDto dto, CancellationToken cancellationToken = default);
}
