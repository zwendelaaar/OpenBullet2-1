﻿using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OpenBullet2.Entities;
using OpenBullet2.Models.Jobs;
using OpenBullet2.Repositories;
using OpenBullet2.Services;
using RuriLib.Models.Jobs;
using RuriLib.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace OpenBullet2.Pages
{
    public partial class JobCreator
    {
        [Inject] IJobRepository JobRepo { get; set; }
        [Inject] SingletonDbHitRepository HitRepo { get; set; }
        [Inject] JobManagerService Manager { get; set; }
        [Inject] ConfigService ConfigService { get; set; }
        [Inject] RuriLibSettingsService RuriLibSettings { get; set; }
        [Inject] NavigationManager Nav { get; set; }
        [Parameter] public string Type { get; set; }

        Job job;

        protected override void OnInitialized()
        {
            Type ??= JobType.MultiRun.ToString();

            var factory = new JobFactory(ConfigService, RuriLibSettings, HitRepo);
            var type = (JobType)Enum.Parse(typeof(JobType), Type);
            job = factory.CreateNew(type);
        }

        private async Task Create()
        {
            var factory = new JobOptionsFactory();
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            var wrapper = new JobOptionsWrapper { Options = factory.GetOptions(job) };

            var entity = new JobEntity
            {
                CreationDate = DateTime.Now,
                JobType = GetJobType(job),
                JobOptions = JsonConvert.SerializeObject(wrapper, settings)
            };

            await JobRepo.Add(entity);

            // Get the entity that was just added in order to get its ID
            entity = await JobRepo.GetAll().OrderByDescending(e => e.Id).FirstAsync();

            job.Id = entity.Id;
            Manager.Jobs.Add(job);
            Nav.NavigateTo($"job/{job.Id}");
        }

        private JobType GetJobType(Job job)
        {
            return job switch
            {
                SingleRunJob _ => JobType.SingleRun,
                MultiRunJob _ => JobType.MultiRun,
                RipJob _ => JobType.Ripper,
                SpiderJob _ => JobType.Spider,
                SeleniumUnitTestJob _ => JobType.SeleniumUnitTest,
                _ => throw new NotImplementedException()
            };
        }
    }
}
