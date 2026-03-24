using MulhollandRealEstate.API.Models;

namespace MulhollandRealEstate.API.Services;

public interface ITriageService
{
    Task<TriageResult> TriageAsync(MaintenanceRequest ticket, CancellationToken cancellationToken = default);
}
