using HotelAutomation.Core.Models;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HotelAutomation.Worker
{
    public class JobProcessor : BackgroundService
    {
        private readonly JobRepository _jobRepository;
        private readonly IOutboxRepository _outboxRepository;
        private readonly Services.VariantSelector _variantSelector;
        private readonly ILogger<JobProcessor> _logger;

        public JobProcessor(JobRepository jobRepository, IOutboxRepository outboxRepository, Services.VariantSelector variantSelector, ILogger<JobProcessor> logger)
        {
            _jobRepository = jobRepository;
            _outboxRepository = outboxRepository;
            _variantSelector = variantSelector;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("JobProcessor started.");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                int processedCount = 0;
                try
                {
                    var ripeJobs = (await _jobRepository.GetRipeJobsAsync(limit: 50)).ToList();
                    processedCount = ripeJobs.Count;

                    foreach (var job in ripeJobs)
                    {
                        var variantId = await _variantSelector.SelectVariantIdAsync(job.TemplateId);
                        var outboxMessage = new OutboxMessage
                        {
                            ToPhoneNumber = job.PhoneNumber,
                            TemplateId = job.TemplateId,
                            VariantId = variantId,
                            ParametersJson = job.ParametersJson,
                            Status = "Pending",
                            CreatedAt = DateTimeOffset.UtcNow,
                            RuleId = job.RuleId
                        };

                        await _outboxRepository.AddAsync(outboxMessage);
                        await _jobRepository.MarkAsProcessedAsync(job.Id);
                        
                        _logger.LogInformation("Job {Id} processed and moved to Outbox", job.Id);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing scheduled jobs.");
                }

                try
                {
                    // If jobs were found and processed, check next batch quickly (500ms); otherwise back off 3s to reduce SQLite contention
                    var delay = processedCount > 0 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(3);
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    // Graceful shutdown — token was cancelled, exit the loop cleanly
                    break;
                }
            }
        }
    }
}
