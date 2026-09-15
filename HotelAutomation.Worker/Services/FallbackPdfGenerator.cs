namespace HotelAutomation.Worker.Services;

using System;
using System.Threading;
using System.Threading.Tasks;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Core.Models;

/// <summary>
/// Safe fallback PDF generator used when native QuestPdfSkia libraries are unavailable.
/// Ensures the hotel automation service continues operating even if native dependencies cannot be loaded.
/// </summary>
public sealed class FallbackPdfGenerator : IPdfGenerator
{
    public Task<byte[]> GenerateInvoicePdfAsync(ScheduledJob job, CancellationToken ct = default)
    {
        // Return empty payload; caller falls back to sending invoice notification without PDF attachment
        return Task.FromResult(Array.Empty<byte>());
    }
}
