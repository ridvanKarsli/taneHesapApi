using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Suppliers;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Tedarikçi kartları, alışları ve ödemeleri (borç takibi) — ADMIN yönetir, EMPLOYEE görüntüler.
/// bkz. Proje Raporu bölüm 3.12.
/// </summary>
[ApiController]
[Route("api/suppliers")]
[Authorize(Roles = "Admin,Employee")]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;
    private readonly ICurrentUserService _currentUserService;

    public SuppliersController(ISupplierService supplierService, ICurrentUserService currentUserService)
    {
        _supplierService = supplierService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<SupplierDto>>> GetAll(CancellationToken ct)
        => Ok(await _supplierService.GetAllAsync(BusinessId, ct));

    [HttpGet("debt-summary")]
    public async Task<ActionResult<decimal>> GetTotalOutstandingDebt(CancellationToken ct)
        => Ok(await _supplierService.GetTotalOutstandingDebtAsync(BusinessId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SupplierDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _supplierService.GetByIdAsync(BusinessId, id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SupplierDto>> Create([FromBody] CreateSupplierRequest request, CancellationToken ct)
    {
        var created = await _supplierService.CreateAsync(BusinessId, request, UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SupplierDto>> Update(Guid id, [FromBody] UpdateSupplierRequest request, CancellationToken ct)
        => Ok(await _supplierService.UpdateAsync(BusinessId, id, request, UserId, ct));

    [HttpGet("{supplierId:guid}/purchases")]
    public async Task<ActionResult<List<SupplierPurchaseDto>>> GetPurchases(Guid supplierId, CancellationToken ct)
        => Ok(await _supplierService.GetPurchasesAsync(BusinessId, supplierId, ct));

    [HttpPost("{supplierId:guid}/purchases")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SupplierPurchaseDto>> AddPurchase(Guid supplierId, [FromBody] CreateSupplierPurchaseRequest request, CancellationToken ct)
    {
        var created = await _supplierService.AddPurchaseAsync(BusinessId, supplierId, request, UserId, ct);
        return Ok(created);
    }

    [HttpPost("purchases/{purchaseId:guid}/payments")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SupplierPurchaseDto>> AddPayment(Guid purchaseId, [FromBody] CreateSupplierPaymentRequest request, CancellationToken ct)
    {
        var updated = await _supplierService.AddPaymentAsync(BusinessId, purchaseId, request, UserId, ct);
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _supplierService.DeleteAsync(BusinessId, id, ct);
        return NoContent();
    }
}
