using System;
using System.Collections.Generic;
using DataModel;
using Microsoft.AspNetCore.Mvc;

namespace WebAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SimulatorController : ControllerBase
    {
        private static List<ProcessStatusEvent> stamps = null;

        public SimulatorController()
        {
            if (stamps == null)
            {
                stamps = new List<ProcessStatusEvent>();
            }
        }

        [HttpGet]
        public IEnumerable<ProcessStatusEvent> Get()
        {
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            return stamps;
        }

        [HttpPost]
        public void Post([FromBody] ProcessStatusEvent value)
        {
            Response.Headers.Add("Access-Control-Allow-Origin", "*");
            stamps.Add(value);
        }
    }
}