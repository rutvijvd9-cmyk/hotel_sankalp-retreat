using HotelAutomation.Core.Models;
using HotelAutomation.Core.Interfaces;
using HotelAutomation.Data.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HotelAutomation.Worker
{
    public class JobProcessor : BackgroundService
    {
        private readonly JobRepository _jobRepository;
        private readonly IOutboxRepository _outboxRepository;
        private readonly ILogger<JobProcessor> _logger;

        public JobProcessor(JobRepository jobRepository, IOutboxRepository outboxRepository, ILogger<JobProcessor> logger)
        {
            _jobRepository = jobRepository;
            _outboxRepository = outboxRepository;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("JobProcessor started.");
            
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var ripeJobs = await _jobRepository.GetRipeJobsAsync();
                    foreach (var job in ripeJobs)
                    {
                        var outboxMessage = new OutboxMessage
                        {
                            ToPhoneNumber = job.PhoneNumber,
                            TemplateId = job.TemplateId,
                            ParametersJson = System.Text.Json.JsonSerializer.Serialize(new List<string> { job.GuestName }),
                            Status = "Pending",
                            CreatedAt = DateTimeOffset.UtcNow
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

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}
