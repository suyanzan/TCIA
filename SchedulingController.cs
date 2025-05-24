using System;
using System.Collections.Generic;
using System.Web.Http;

namespace YourNamespace.Controllers
{
    public class AnalyzeRequest
    {
        public string InputContext { get; set; }
    }

    public class MonthlyInjectionResult
    {
        public string Type { get; set; } = "monthly_injection";
        public string StatisticInterval { get; set; }
        public int MonthlyInjection { get; set; }
        public int DelayDays { get; set; }
        public string UtilizationRate { get; set; }
        public List<string> RecommendedActions { get; set; }
    }

    public class OpcCapacity
    {
        public string Date { get; set; }
        public int AvailableOpcCapacity { get; set; }
    }

    public class ReticleOrder
    {
        public string Id { get; set; }
        public string DueDate { get; set; }
        public int RequiredOpc { get; set; }
    }

    public class OpcAndReticleData
    {
        public string Type { get; set; } = "opc_analysis";
        public List<OpcCapacity> OpcCapacity { get; set; }
        public List<ReticleOrder> ReticleOrders { get; set; }
    }

    [RoutePrefix("Scheduling")]
    public class SchedulingController : ApiController
    {
        [HttpPost]
        [Route("Analyze")]
        public IHttpActionResult Analyze([FromBody] AnalyzeRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.InputContext))
                return BadRequest("Invalid input.");

            // 根據輸入內容回傳不同資料
            if (request.InputContext.Contains("投放"))
            {
                var result = new MonthlyInjectionResult
                {
                    StatisticInterval = "04/26~05/25",
                    MonthlyInjection = 2500,
                    DelayDays = 4,
                    UtilizationRate = "88%",
                    RecommendedActions = new List<string>
                    {
                        "延遲天數超過3天，請與MP單位確認交期",
                        "使用率低於90%，需注意OPC工作區後續安排"
                    }
                };

                return Ok(result);
            }
            else
            {
                var result = new OpcAndReticleData
                {
                    OpcCapacity = new List<OpcCapacity>
                    {
                        new OpcCapacity { Date = "2025-05-24", AvailableOpcCapacity = 3100 },
                        new OpcCapacity { Date = "2025-05-25", AvailableOpcCapacity = 3000 }
                    },
                    ReticleOrders = new List<ReticleOrder>
                    {
                        new ReticleOrder { Id = "R-001", DueDate = "2025-05-25", RequiredOpc = 1800 },
                        new ReticleOrder { Id = "R-002", DueDate = "2025-05-26", RequiredOpc = 1500 }
                    }
                };

                return Ok(result);
            }
        }
    }
}
