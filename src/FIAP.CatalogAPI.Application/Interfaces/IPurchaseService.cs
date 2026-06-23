using FIAP.CatalogAPI.Application.DTOs;

namespace FIAP.CatalogAPI.Application.Interfaces;

public interface IPurchaseService
{
    Task<PurchaseResponseDto> InitiatePurchaseAsync(PurchaseRequestDto dto, Guid userId, CancellationToken cancellationToken = default);
}
