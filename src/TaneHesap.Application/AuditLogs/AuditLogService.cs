using TaneHesap.Application.Common.Interfaces;
using TaneHesap.Domain.Entities;

namespace TaneHesap.Application.AuditLogs;

public class AuditLogService : IAuditLogService
{
    private readonly IUnitOfWork _unitOfWork;

    public AuditLogService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<List<AuditLogDto>> GetLogsAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var logs = await _unitOfWork.Repository<AuditLog>().ListAsync(l =>
            (query.BusinessId == null || l.BusinessId == query.BusinessId) &&
            (query.EntityName == null || l.EntityName == query.EntityName) &&
            (query.FromUtc == null || l.TimestampUtc >= query.FromUtc) &&
            (query.ToUtc == null || l.TimestampUtc <= query.ToUtc), ct);

        return logs
            .OrderByDescending(l => l.TimestampUtc)
            .Select(ToDto)
            .ToList();
    }

    private static AuditLogDto ToDto(AuditLog l) => new(
        l.Id, l.BusinessId, l.UserId, l.ActionType, l.EntityName, l.EntityId, l.OldValuesJson, l.NewValuesJson, l.TimestampUtc);
}
