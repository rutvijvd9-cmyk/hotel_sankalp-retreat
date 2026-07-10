namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;

public interface ITemplateRepository
{
    Task<MessageTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<MessageTemplate> Templates, int TotalCount)> GetAllPaginatedAsync(int page = 1, int limit = 50, CancellationToken cancellationToken = default);
    Task<int> AddAsync(MessageTemplate template, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(int id, MessageTemplate template, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
