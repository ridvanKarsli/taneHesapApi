using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Platforms;

namespace TaneHesap.API.Controllers;

/// <summary>
/// Paket servis platformları (Yemeksepeti, Getir vb.) ve komisyon yüzdeleri — ADMIN yönetir,
/// EMPLOYEE görüntüler. bkz. Proje Raporu bölüm 3.4.
/// </summary>
[ApiController]
[Route("api/platforms")]
[Authorize(Roles = "Admin,Employee")]
public class PlatformsController : ControllerBase
{
    private readonly IPlatformService _platformService;
    private readonly ICurrentUserService _currentUserService;

    public PlatformsController(IPlatformService platformService, ICurrentUserService currentUserService)
    {
        _platformService = platformService;
        _currentUserService = currentUserService;
    }

    private Guid BusinessId => _currentUserService.BusinessId!.Value;
    private Guid UserId => _currentUserService.UserId!.Value;

    [HttpGet]
    public async Task<ActionResult<List<PlatformDto>>> GetAll(CancellationToken ct)
        => Ok(await _platformService.GetAllAsync(BusinessId, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PlatformDto>> GetById(Guid id, CancellationToken ct)
        => Ok(await _platformService.GetByIdAsync(BusinessId, id, ct));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlatformDto>> Create([FromBody] CreatePlatformRequest request, CancellationToken ct)
    {
        var created = await _platformService.CreateAsync(BusinessId, request, UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<PlatformDto>> Update(Guid id, [FromBody] UpdatePlatformRequest request, CancellationToken ct)
        => Ok(await _platformService.UpdateAsync(BusinessId, id, request, UserId, ct));
}
