namespace HotelAutomation.Core.Interfaces;

using HotelAutomation.Core.Models;
using System.Threading;
using System.Threading.Tasks;

public interface IPdfGenerator
{
    Task<byte[]> GenerateInvoicePdfAsync(ScheduledJob job, CancellationToken ct = default);
}
