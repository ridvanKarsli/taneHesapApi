using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Businesses;
using TaneHesap.Application.Common.Interfaces;

namespace TaneHesap.API.Controllers;

/// <summary>
/// İşletme (Business) yönetimi — sadece SUPER_ADMIN. bkz. Proje Raporu bölüm 2.
/// </summary>
[ApiController]
[Route("api/businesses")]
[Authorize(Roles = "SuperAdmin")]
public class BusinessesController : ControllerBase
{
    private readonly IBusinessService _businessService;
    private readonly ICurrentUserService _currentUserService;

    public BusinessesController(IBusinessService businessService, ICurrentUserService currentUserService)
    {
        _businessService = businessService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<List<BusinessDto>>> GetAll(CancellationToken ct)
        => Ok(await _businessService.GetAllAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BusinessDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _businessService.GetByIdAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<BusinessDto>> Create([FromBody] CreateBusinessRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        var created = await _businessService.CreateAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BusinessDto>> Update(Guid id, [FromBody] UpdateBusinessRequest request, CancellationToken ct)
    {
        var userId = _currentUserService.UserId!.Value;
        return Ok(await _businessService.UpdateAsync(id, request, userId, ct));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _businessService.DeleteAsync(id, ct);
        return NoContent();
    }
}
