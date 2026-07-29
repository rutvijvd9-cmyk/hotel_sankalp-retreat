using HotelAutomation.Connectors.WhatsApp;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;

namespace HotelAutomation.Worker.Services;

public sealed class ConfigureWhatsAppOptions : IConfigureOptions<WhatsAppOptions>
{
    private readonly IServiceProvider _serviceProvider;

    public ConfigureWhatsAppOptions(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public void Configure(WhatsAppOptions options)
    {
        using var scope = _serviceProvider.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<AppConfigRepository>();

        var accessToken = repo.GetValueAsync("WhatsApp_AccessToken").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(accessToken)) options.AccessToken = accessToken;

        var phoneNumberId = repo.GetValueAsync("WhatsApp_PhoneNumberId").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(phoneNumberId)) options.PhoneNumberId = phoneNumberId;

        var apiVersion = repo.GetValueAsync("WhatsApp_ApiVersion").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(apiVersion)) options.ApiVersion = apiVersion;

        var verifyToken = repo.GetValueAsync("WhatsApp_VerifyToken").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(verifyToken)) options.VerifyToken = verifyToken;

        var wabaId = repo.GetValueAsync("WhatsApp_WhatsAppBusinessAccountId").GetAwaiter().GetResult();
        if (!string.IsNullOrEmpty(wabaId)) options.WhatsAppBusinessAccountId = wabaId;
    }
}
