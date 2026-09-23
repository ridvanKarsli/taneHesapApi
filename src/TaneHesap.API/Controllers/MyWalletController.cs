using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Application.Employees;

namespace TaneHesap.API.Controllers;

/// <summary>EMPLOYEE'nin kendi cüzdanını görmesi (salt okunur) — bkz. Proje Raporu bölüm 2, 3.7.</summary>
[ApiController]
[Route("api/me/wallet")]
[Authorize(Roles = "Employee")]
public class MyWalletController : ControllerBase
{
    private readonly IEmployeeWalletService _walletService;
    private readonly ICurrentUserService _currentUserService;

    public MyWalletController(IEmployeeWalletService walletService, ICurrentUserService currentUserService)
    {
        _walletService = walletService;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<ActionResult<EmployeeWalletDto>> Get(CancellationToken ct)
        => Ok(await _walletService.GetWalletAsync(_currentUserService.BusinessId!.Value, _currentUserService.UserId!.Value, ct));
}
