using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Treasury;
using TaneHesap.Domain.Enums;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Kasa: nakit kasası, kart kasası (banka) ve kredi kartları — bakiye, transfer, kart ödemesi, düzeltme.
/// Sadece ADMIN. bkz. Proje Raporu bölüm 3.15.
/// </summary>
[ApiController]
[Route("api/treasury")]
[Authorize(Roles = "Admin")]
public class TreasuryController : ControllerBase
{
    private readonly ITreasuryService _treasuryService;
    private readonly ICurrentUserService _currentUserService;

    public TreasuryController(ITreasuryService treasuryService, ICurrentUserService currentUserService)
    {
        _treasuryService = treasuryService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpGet("summary")]
    public async Task<ActionResult<TreasurySummaryDto>> GetSummary(CancellationToken ct)
        => Ok(await _treasuryService.GetSummaryAsync(BusinessId, ct));

    [HttpPut("settings")]
    public async Task<ActionResult<TreasurySummaryDto>> UpdateSettings([FromBody] UpdateTreasurySettingsRequest request, CancellationToken ct)
        => Ok(await _treasuryService.UpdateSettingsAsync(BusinessId, request, UserId, ct));

    [HttpGet("transactions")]
    public async Task<ActionResult<List<TreasuryTransactionDto>>> GetTransactions(
        [FromQuery] DateOnly? fromDate, [FromQuery] DateOnly? toDate, [FromQuery] TreasuryAccount? account, [FromQuery] Guid? paymentCardId, CancellationToken ct)
        => Ok(await _treasuryService.GetTransactionsAsync(BusinessId, new TreasuryTransactionFilter(fromDate, toDate, account, paymentCardId), ct));

    [HttpDelete("transactions/{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid id, CancellationToken ct)
    {
        await _treasuryService.DeleteTransactionAsync(BusinessId, id, ct);
        return NoContent();
    }

    [HttpPost("transfers")]
    public async Task<ActionResult<List<TreasuryTransactionDto>>> Transfer([FromBody] TransferRequest request, CancellationToken ct)
        => Ok(await _treasuryService.TransferAsync(BusinessId, request, UserId, ct));

    [HttpPost("card-payments")]
    public async Task<ActionResult<List<TreasuryTransactionDto>>> PayCard([FromBody] CardPaymentRequest request, CancellationToken ct)
        => Ok(await _treasuryService.PayCardAsync(BusinessId, request, UserId, ct));

    [HttpPost("adjustments")]
    public async Task<ActionResult<TreasuryTransactionDto>> Adjust([FromBody] ManualAdjustmentRequest request, CancellationToken ct)
        => Ok(await _treasuryService.AdjustAsync(BusinessId, request, UserId, ct));

    /// <summary>EMPLOYEE de gider girerken "hangi kart" seçebilmek için kart listesini görür (bakiye/kasa değil).</summary>
    [HttpGet("cards")]
    [Authorize(Roles = "Admin,Employee")]
    public async Task<ActionResult<List<PaymentCardDto>>> GetCards(CancellationToken ct)
        => Ok(await _treasuryService.GetCardsAsync(BusinessId, ct));

    [HttpPost("cards")]
    public async Task<ActionResult<PaymentCardDto>> CreateCard([FromBody] CreatePaymentCardRequest request, CancellationToken ct)
        => Ok(await _treasuryService.CreateCardAsync(BusinessId, request, UserId, ct));

    [HttpPut("cards/{id:guid}")]
    public async Task<ActionResult<PaymentCardDto>> UpdateCard(Guid id, [FromBody] UpdatePaymentCardRequest request, CancellationToken ct)
        => Ok(await _treasuryService.UpdateCardAsync(BusinessId, id, request, UserId, ct));

    [HttpDelete("cards/{id:guid}")]
    public async Task<IActionResult> DeleteCard(Guid id, CancellationToken ct)
    {
        await _treasuryService.DeleteCardAsync(BusinessId, id, ct);
        return NoContent();
    }
}
