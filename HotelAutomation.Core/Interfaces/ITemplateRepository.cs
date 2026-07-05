namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;

public interface ITemplateRepository
{
    Task<MessageTemplate?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MessageTemplate>> GetAllAsync(CancellationToken cancellationToken = default);
}
