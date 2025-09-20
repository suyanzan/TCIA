using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using static DashboardNew.Controllers.DashboardNewController;

namespace DashboardNew.Models
{

    public class DashboardViewModel
    {
        /// <summary>
        /// 目標光罩編號 (例如 F307-AR40H8)
        /// </summary>
        public string TargetMaskId { get; set; }

        /// <summary>
        /// 各方案對應的圖表資料 (maskId + delay)
        /// A: 目標 + 延遲最嚴重前2名
        /// B: 目標 + 延遲最少前2名
        /// C: 全部
        /// </summary>
        public Dictionary<string, List<ScenarioDelayItem>> Scenarios { get; set; }

        /// <summary>
        /// 三方案比較表 (總遞延天數 / 平均遞延 / 額外OPC需求)
        /// </summary>
        public List<ComparisonRow> Comparison { get; set; }

        /// <summary>
        /// 延遲完整清單 (主要給表格顯示用)
        /// </summary>
        public Dictionary<string, List<DelayInfo>> DelayLists { get; set; }
        // C 案每日容量檢查
        public List<DailyCapacityInfo> DailyCapacityCheck { get; set; }
        // 👉 新增 ExtraInfo
        public ExtraInfo ExtraInfo { get; set; }
        // 👉 新增 C 方案的每日 OPC 需求清單
        public List<DailyCapacityInfo> OpcDemandList { get; set; }
    }


    public class ExtraInfo
    {
        public TargetJob TargetJob { get; set; }
        public MaxDayInfo MaxDayInfo { get; set; }
    }

    public class TargetJob
    {
        public string mask_id { get; set; }
        public string due_date { get; set; }
        public int opc_demand { get; set; }
    }

    public class MaxDayInfo
    {
        public string date { get; set; }
        public int demand { get; set; }
        public int capacity { get; set; }
        public int gap { get; set; }
    }
    // === 子 DTOs ===

    public class ScenarioDelayItem
    {
        public string maskId { get; set; }
        public int delay { get; set; }
    }

    public class ComparisonRow
    {
        public string strategy { get; set; }
        public int total_delay_days { get; set; }
        public double avg_delay_days { get; set; }
        public int extra_opc_needed { get; set; }
        public int max_gap { get; set; }   // 👉 新增最大缺口
    }

    public class DelayInfo
    {
        public string mask_id { get; set; }
        public int delay_days { get; set; }
    }
    // ===== DTOs =====
    public class JobItem { public string mask_id { get; set; } public string due_date { get; set; } public int opc_demand { get; set; } }

    public class SolutionJob { public string mask_id { get; set; } public string old_date { get; set; } public string new_date { get; set; } public int opc_demand { get; set; } public int opc_capacity { get; set; } public int delay_days { get; set; } public string note { get; set; } public List<DailyAlloc> allocations { get; set; } }
    public class DailyAlloc { public string date { get; set; } public int amount { get; set; } public int capacity_before { get; set; } }
    public class AllocationResult { public List<DailyAlloc> allocs { get; set; } public string finishDate { get; set; } public int delayDays { get; set; } public int extraOpcUsed { get; set; } }
}