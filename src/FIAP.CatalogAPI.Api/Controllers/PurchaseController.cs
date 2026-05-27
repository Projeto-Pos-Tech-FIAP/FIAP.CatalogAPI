using FIAP.CatalogAPI.Application.DTOs;
using FIAP.CatalogAPI.Application.Interfaces;
using FIAP.CatalogAPI.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace FIAP.CatalogAPI.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class PurchaseController : ControllerBase
{
    private readonly IPurchaseService _purchaseService;

    public PurchaseController(IPurchaseService purchaseService) => _purchaseService = purchaseService;

    /// <summary>
    /// Inicia o fluxo de compra de um jogo.
    /// Publica OrderPlacedEvent no Kafka e aguarda PaymentProcessedEvent.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PurchaseResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ExceptionOutputDto), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiatePurchaseAsync(
        [FromBody] PurchaseRequestDto dto,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _purchaseService.InitiatePurchaseAsync(dto, cancellationToken);
            return Accepted(response);
        }
        catch (NotFoundException ex)
        {
            return NotFound(new ExceptionOutputDto { Message = ex.Message, StatusCode = 404 });
        }
        catch (GameAlreadyOwnedException ex)
        {
            return BadRequest(new ExceptionOutputDto { Message = ex.Message, StatusCode = 400 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ExceptionOutputDto { Message = ex.Message, StatusCode = 400 });
        }
    }
}
