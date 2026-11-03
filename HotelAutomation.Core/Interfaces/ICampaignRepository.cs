namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ICampaignRepository
{
    Task<IReadOnlyList<Campaign>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Campaign?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<int> CreateAsync(Campaign campaign, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, Campaign campaign, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<int> ClearOldProgressAsync(int olderThanDays = 15, CancellationToken cancellationToken = default);
}
