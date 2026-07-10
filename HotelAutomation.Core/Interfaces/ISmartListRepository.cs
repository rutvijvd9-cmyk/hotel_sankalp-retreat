namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface ISmartListRepository
{
    Task<IReadOnlyList<SmartList>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<int> AddAsync(SmartList list, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<SmartList?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
