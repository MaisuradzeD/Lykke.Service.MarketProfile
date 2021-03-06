﻿using System;
using Lykke.Service.MarketProfile.Models.IsAlive;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.PlatformAbstractions;

namespace Lykke.Service.MarketProfile.Controllers
{
    /// <summary>
    /// Controller to test service is alive
    /// </summary>
    [Route("api/[controller]")]
    public class IsAliveController : Controller
    {
        /// <summary>
        /// Checks service is alive
        /// </summary>
        [HttpGet]
        public IsAliveResponse Get()
        {
            return new IsAliveResponse
            {
                Version = PlatformServices.Default.Application.ApplicationVersion,
                Env = Environment.GetEnvironmentVariable("ENV_INFO")
            };
        }
    }
}