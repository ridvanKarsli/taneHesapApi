using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Admins;

namespace TaneHesap.API.Controllers;

/// <summary>
/// SUPER_ADMIN'in bir işletmeye ADMIN (işletme sahibi) kullanıcı ekleyip yönetmesi.
/// bkz. Proje Raporu bölüm 2.
/// </summary>
[ApiController]
[Route("api/businesses/{businessId:guid}/admins")]
[Authorize(Roles = "SuperAdmin")]
public class AdminsController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpGet]
    public async Task<ActionResult<List<AdminDto>>> GetAll(Guid businessId, CancellationToken ct)
        => Ok(await _adminService.GetAllAsync(businessId, ct));

    [HttpPost]
    public async Task<ActionResult<AdminDto>> Create(Guid businessId, [FromBody] CreateAdminRequest request, CancellationToken ct)
        => Ok(await _adminService.CreateAsync(businessId, request, ct));

    [HttpPut("{adminId:guid}")]
    public async Task<ActionResult<AdminDto>> Update(Guid businessId, Guid adminId, [FromBody] UpdateAdminRequest request, CancellationToken ct)
        => Ok(await _adminService.UpdateAsync(businessId, adminId, request, ct));
}
