namespace HotelAutomation.Worker.Services;

using HotelAutomation.Core.Models;
using HotelAutomation.Data.Repositories;
using System;
using System.Linq;
using System.Threading.Tasks;

public sealed class VariantSelector
{
    private readonly TemplateVariantRepository _variantRepository;
    private readonly Random _random = new();

    public VariantSelector(TemplateVariantRepository variantRepository)
    {
        _variantRepository = variantRepository;
    }

    public async Task<int?> SelectVariantIdAsync(int templateId)
    {
        var variants = await _variantRepository.GetByTemplateIdAsync(templateId);
        if (variants.Count == 0) return null;

        int totalWeight = variants.Sum(v => v.Weight);
        if (totalWeight <= 0) return null; // Avoid divide by zero

        int roll = _random.Next(totalWeight);
        int currentWeight = 0;

        foreach (var variant in variants)
        {
            currentWeight += variant.Weight;
            if (roll < currentWeight)
            {
                return variant.Id;
            }
        }

        return variants.Last().Id;
    }
}
