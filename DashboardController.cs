//using DashboardNew.Models;
using DashboardNew.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace DashboardNew.Controllers
{
    public class DashboardNewController : Controller
    {
        public ActionResult Index(string targetMask, int shift_days)
        {
            System.Diagnostics.Debug.WriteLine(">>> Index 執行中 <<<");
            System.Diagnostics.Debug.WriteLine($">>> Index 執行中, targetMask={targetMask}, shift_days={shift_days}");
            // ====== 假資料：OPC 每天固定 3233 (2025/09/19 ~ 2025/09/25) ======
            var opcCap = new Dictionary<string, int>();
            for (int i = 0; i < 30; i++)
            {
                string date = new DateTime(2025, 9, 19).AddDays(i).ToString("yyyy-MM-dd");
                opcCap[date] = 3233;
            }

            // ====== 假資料：15 筆光罩 ======
            var affectedJobs = new List<JobItem>
            {
                new JobItem { mask_id="F300-AR60A1", due_date="2025-09-19", opc_demand=1653 },
                new JobItem { mask_id="F301-AR70B2", due_date="2025-09-19", opc_demand=780 },
                new JobItem { mask_id="F302-AR80C3", due_date="2025-09-19", opc_demand=922 },
                new JobItem { mask_id="F303-AR90D4", due_date="2025-09-20", opc_demand=2050 },
                new JobItem { mask_id="F304-AR10E5", due_date="2025-09-20", opc_demand=1199 },
                new JobItem { mask_id="F305-AR20F6", due_date="2025-09-20", opc_demand=333 },
                new JobItem { mask_id="F306-AR30G7", due_date="2025-09-21", opc_demand=477 },
                new JobItem { mask_id="F307-AR40H8", due_date="2025-09-21", opc_demand=1693 }, // 🎯目標光罩
                new JobItem { mask_id="F308-AR50I9", due_date="2025-09-21", opc_demand=735 },
                new JobItem { mask_id="F309-AR60J0", due_date="2025-09-21", opc_demand=2850 },
                new JobItem { mask_id="F310-AR11K1", due_date="2025-09-22", opc_demand=288 },
                new JobItem { mask_id="F311-AR12L2", due_date="2025-09-22", opc_demand=3105 },
                new JobItem { mask_id="F312-AR13M3", due_date="2025-09-23", opc_demand=2250 },
                new JobItem { mask_id="F313-AR14N4", due_date="2025-09-23", opc_demand=1400 },
                new JobItem { mask_id="F396-AR60A1", due_date="2025-09-24", opc_demand=2959 }
            };

            //string targetMask = "F307-AR40H8";
            //int shift_days = 2;

            // ====== 三方案計算 ======
            var solA = BuildPriorityFirst(targetMask, affectedJobs, new Dictionary<string, int>(opcCap), shift_days);
            var solB = BuildAverageShift(targetMask, affectedJobs, new Dictionary<string, int>(opcCap), shift_days - 1);
            var solC = BuildResourceReallocation(targetMask, affectedJobs, new Dictionary<string, int>(opcCap), shift_days);

            // ====== 比較表 ======
            var comparison = new List<ComparisonRow>
            {
                new ComparisonRow { strategy="A", total_delay_days=solA.jobs.Sum(j=>j.delay_days), avg_delay_days=solA.jobs.Average(j=>j.delay_days), extra_opc_needed=0 },
                new ComparisonRow { strategy="B", total_delay_days=solB.jobs.Sum(j=>j.delay_days), avg_delay_days=solB.jobs.Average(j=>j.delay_days), extra_opc_needed=0 },
                new ComparisonRow { strategy="C", total_delay_days=solC.jobs.Sum(j=>j.delay_days), avg_delay_days=solC.jobs.Average(j=>j.delay_days), extra_opc_needed=solC.extra_opc_needed, max_gap=solC.maxGap }
            };

            // ====== ViewModel ======
            var model = new DashboardViewModel
            {
                TargetMaskId = targetMask,
                Scenarios = new Dictionary<string, List<ScenarioDelayItem>>
                {
                    ["方案A"] = GetChartDataForA(solA, targetMask),
                    ["方案B"] = GetChartDataForB(solB, targetMask),
                    ["方案C"] = solC.jobs
                                    .OrderBy(j => DateTime.Parse(j.old_date))
                                    .Take(2) // 最前面兩筆
                                    .Select(j => new ScenarioDelayItem { maskId = j.mask_id, delay = j.delay_days })
                                    .ToList()
                },
                Comparison = comparison,
                DelayLists = new Dictionary<string, List<DelayInfo>>
                {
                    ["方案A"] = solA.delayList,
                    ["方案B"] = solB.delayList
                },
                ExtraInfo = new ExtraInfo
                {
                    TargetJob = new TargetJob
                    {
                        mask_id = targetMask,
                        due_date = affectedJobs.First(j => j.mask_id == targetMask).due_date,
                        opc_demand = affectedJobs.First(j => j.mask_id == targetMask).opc_demand
                    },
                    MaxDayInfo = new MaxDayInfo
                    {
                        date = solC.maxDemandDay,
                        demand = solC.dailyCapacityCheck.First(d => d.date == solC.maxDemandDay).demand,
                        capacity = solC.dailyCapacityCheck.First(d => d.date == solC.maxDemandDay).capacity,
                        gap = solC.maxGap
                    }
                },
                // 👉 新增 C 案 OPC需求清單
                OpcDemandList = solC.dailyCapacityCheck
            };

            return View(model); // 回傳到原本的大師 View
        }
        // === 策略 A ===
        private Solution BuildPriorityFirst(string targetMask, List<JobItem> jobs, Dictionary<string, int> opcCap, int shift)
        {
            var sol = new Solution { summary = "目標提前必準時，其他依序延後", jobs = new List<SolutionJob>() };

            var targetJob = jobs.First(j => j.mask_id == targetMask);
            DateTime targetDue = DateTime.Parse(targetJob.due_date).AddDays(-shift);

            var targetAlloc = Allocate(targetJob.opc_demand, opcCap, targetDue);
            sol.jobs.Add(new SolutionJob
            {
                mask_id = targetJob.mask_id,
                old_date = targetJob.due_date,
                new_date = targetAlloc.finishDate,
                opc_demand = targetJob.opc_demand,
                opc_capacity = targetAlloc.allocs.Last().capacity_before,
                delay_days = targetAlloc.delayDays,
                note = "目標工單必準時",
                allocations = targetAlloc.allocs
            });

            var others = jobs.Where(j => j.mask_id != targetMask).OrderBy(j => DateTime.Parse(j.due_date));

            foreach (var job in others)
            {
                DateTime due = DateTime.Parse(job.due_date);
                DateTime start = DateTime.Parse(targetAlloc.finishDate);
                if (due < start) due = start;

                var alloc = Allocate(job.opc_demand, opcCap, due);
                sol.jobs.Add(new SolutionJob
                {
                    mask_id = job.mask_id,
                    old_date = job.due_date,
                    new_date = alloc.finishDate,
                    opc_demand = job.opc_demand,
                    opc_capacity = alloc.allocs.Last().capacity_before,
                    delay_days = alloc.delayDays,
                    note = (alloc.delayDays > 0 ? "延遲" : "準時"),
                    allocations = alloc.allocs
                });
            }

            return sol;
        }
        // === 策略 B ===
        private Solution BuildAverageShift(string targetMask, List<JobItem> jobs, Dictionary<string, int> opcCap, int shift)
        {
            var sol = new Solution { summary = "平均分攤，目標提前 Shift-1 天完成", jobs = new List<SolutionJob>() };

            var targetJob = jobs.First(j => j.mask_id == targetMask);
            DateTime targetDue = DateTime.Parse(targetJob.due_date).AddDays(-shift);

            // 🔹 特殊處理：目標光罩僅允許從 shift-1 天開始分配
            var targetAlloc = Allocate(targetJob.opc_demand, opcCap, targetDue, forceNoDelay: false);

            sol.jobs.Add(new SolutionJob
            {
                mask_id = targetJob.mask_id,
                old_date = targetJob.due_date,
                new_date = targetAlloc.finishDate,
                opc_demand = targetJob.opc_demand,
                opc_capacity = targetAlloc.allocs.Last().capacity_before,
                delay_days = targetAlloc.delayDays+1,
                note = targetAlloc.delayDays > 0 ? "目標光罩遞延" : "目標光罩準時 (Shift-1)",
                allocations = targetAlloc.allocs
            });

            // 其餘工單依照 due_date 排
            var others = jobs.Where(j => j.mask_id != targetMask).OrderBy(j => DateTime.Parse(j.due_date));
            foreach (var job in others)
            {
                DateTime due = DateTime.Parse(job.due_date);
                var alloc = Allocate(job.opc_demand, opcCap, due);

                sol.jobs.Add(new SolutionJob
                {
                    mask_id = job.mask_id,
                    old_date = job.due_date,
                    new_date = alloc.finishDate,
                    opc_demand = job.opc_demand,
                    opc_capacity = alloc.allocs.Last().capacity_before,
                    delay_days = alloc.delayDays,
                    note = (alloc.delayDays > 0 ? "延遲" : "準時"),
                    allocations = alloc.allocs
                });
            }

            return sol;
        }
        // === 策略 C ===
        private Solution BuildResourceReallocation(string targetMask, List<JobItem> jobs, Dictionary<string, int> opcCap, int shift)
        {
            var sol = new Solution { summary = "全部準時", jobs = new List<SolutionJob>(), extra_opc_needed = 0 };
            var demandPerDay = new Dictionary<string, int>();

            foreach (var job in jobs)
            {
                DateTime due = DateTime.Parse(job.due_date);
                if (job.mask_id == targetMask) due = due.AddDays(-shift);

                string key = due.ToString("yyyy-MM-dd");
                if (!demandPerDay.ContainsKey(key)) demandPerDay[key] = 0;
                demandPerDay[key] += job.opc_demand;

                sol.jobs.Add(new SolutionJob
                {
                    mask_id = job.mask_id,
                    old_date = job.due_date,
                    new_date = due.ToString("yyyy-MM-dd"),
                    opc_demand = job.opc_demand,
                    opc_capacity = opcCap.ContainsKey(key) ? opcCap[key] : 0,
                    delay_days = 0,
                    note = "準時",
                    allocations = new List<DailyAlloc>
            {
                new DailyAlloc { date = key, amount = job.opc_demand, capacity_before = opcCap.ContainsKey(key) ? opcCap[key] : 0 }
            }
                });
            }

            // 計算每日缺口，生成清單
            sol.dailyCapacityCheck = demandPerDay.Select(kv =>
            {
                int cap = opcCap.ContainsKey(kv.Key) ? opcCap[kv.Key] : 0;
                int gap = kv.Value - cap;
                string status = gap > 0 ? "溢位" : "充足";
                if (gap > 0) sol.extra_opc_needed += gap;
                return new DailyCapacityInfo
                {
                    date = kv.Key,
                    demand = kv.Value,
                    capacity = cap,
                    status = status,
                    gap = gap
                };
            }).ToList();

            // 標記最大需求日
            var worst = sol.dailyCapacityCheck.OrderByDescending(d => d.gap).FirstOrDefault();
            if (worst != null)
            {
                sol.maxDemandDay = worst.date;
                sol.maxGap = worst.gap;
            }
            return sol;
        }
        // === 分配邏輯 ===
        private AllocationResult Allocate(int demand, Dictionary<string, int> capByDate, DateTime due, bool forceNoDelay = false)
        {
            var allocs = new List<DailyAlloc>();
            int remain = demand;
            DateTime cur = due;
            int extraUsed = 0;
            DateTime maxDate = due.AddDays(30);

            while (remain > 0)
            {
                if (cur > maxDate) throw new InvalidOperationException("需求太大，超出可排程範圍");

                string key = cur.ToString("yyyy-MM-dd");
                if (!capByDate.ContainsKey(key)) capByDate[key] = 0;

                int cap = capByDate[key];
                int take;

                if (forceNoDelay)
                {
                    take = remain;
                    extraUsed += Math.Max(0, take - cap);
                    allocs.Add(new DailyAlloc { date = key, amount = take, capacity_before = cap });
                    capByDate[key] = Math.Max(0, cap - take);
                    remain = 0;
                }
                else
                {
                    take = Math.Min(remain, cap);
                    if (take > 0)
                    {
                        allocs.Add(new DailyAlloc { date = key, amount = take, capacity_before = cap });
                        capByDate[key] -= take;
                        remain -= take;
                    }
                }

                if (remain > 0) cur = cur.AddDays(1);
            }

            DateTime finishDate = DateTime.Parse(allocs.Max(a => a.date));
            int delay = (int)(finishDate - due).TotalDays;

            return new AllocationResult
            {
                allocs = allocs,
                finishDate = finishDate.ToString("yyyy-MM-dd"),
                delayDays = forceNoDelay ? 0 : Math.Max(delay, 0),
                extraOpcUsed = extraUsed
            };
        }

        // === 圖表資料：A (目標 + 延遲最嚴重前 2 名) ===
        public List<ScenarioDelayItem> GetChartDataForA(Solution sol, string targetMask)
        {
            var target = sol.jobs.FirstOrDefault(j => j.mask_id == targetMask);
            var top2 = sol.jobs.Where(j => j.mask_id != targetMask)
                               .OrderByDescending(j => j.delay_days)
                               .Take(2);
            return (new[] { target }).Concat(top2)
                                     .Where(x => x != null)
                                     .Select(j => new ScenarioDelayItem { maskId = j.mask_id, delay = j.delay_days })
                                     .ToList();
        }

        // === 圖表資料：B (目標 + 延遲最少前 2 名) ===
        public List<ScenarioDelayItem> GetChartDataForB(Solution sol, string targetMask)
        {
            var target = sol.jobs.FirstOrDefault(j => j.mask_id == targetMask);
            var low2 = sol.jobs.Where(j => j.mask_id != targetMask)
                               .OrderBy(j => j.delay_days)
                               .Take(2);
            return (new[] { target }).Concat(low2)
                                     .Where(x => x != null)
                                     .Select(j => new ScenarioDelayItem { maskId = j.mask_id, delay = j.delay_days })
                                     .ToList();
        }

        public class DailyCapacityInfo
        {
            public string date { get; set; }
            public int demand { get; set; }
            public int capacity { get; set; }
            public string status { get; set; }
            public int gap { get; set; }
        }
        public class Solution
        {
            public string summary { get; set; }
            public List<SolutionJob> jobs { get; set; }
            public int extra_opc_needed { get; set; }
            public List<DelayInfo> delayList => jobs?.Select(j => new DelayInfo { mask_id = j.mask_id, delay_days = j.delay_days }).ToList() ?? new List<DelayInfo>();
            // 🔹 新增
            public string max_demand_day { get; set; }
            public int max_demand_value { get; set; }
            // 新增每日容量檢查
            public List<DailyCapacityInfo> dailyCapacityCheck { get; set; } = new List<DailyCapacityInfo>();
            // C 案用
            public string maxDemandDay { get; set; }
            public int maxGap { get; set; }
        }
    }

    // ===== DTOs =====
    //public class JobItem { public string mask_id { get; set; } public string due_date { get; set; } public int opc_demand { get; set; } }
    ////public class Solution { public string summary { get; set; } public List<SolutionJob> jobs { get; set; } public int extra_opc_needed { get; set; } }
    //public class SolutionJob { public string mask_id { get; set; } public string old_date { get; set; } public string new_date { get; set; } public int opc_demand { get; set; } public int opc_capacity { get; set; } public int delay_days { get; set; } public string note { get; set; } public List<DailyAlloc> allocations { get; set; } }
    //public class DailyAlloc { public string date { get; set; } public int amount { get; set; } public int capacity_before { get; set; } }
    //public class AllocationResult { public List<DailyAlloc> allocs { get; set; } public string finishDate { get; set; } public int delayDays { get; set; } public int extraOpcUsed { get; set; } }

    //public class Solution
    //{
    //    public string summary { get; set; }
    //    public List<SolutionJob> jobs { get; set; }
    //    public int extra_opc_needed { get; set; }

    //    // 👉 延遲完整清單 (for 前端)
    //    public List<DelayInfo> delayList
    //    {
    //        get
    //        {
    //            return jobs?.Select(j => new DelayInfo
    //            {
    //                mask_id = j.mask_id,
    //                delay_days = j.delay_days
    //            }).ToList() ?? new List<DelayInfo>();
    //        }
    //    }
    //}

    //public class DelayInfo
    //{
    //    public string mask_id { get; set; }
    //    public int delay_days { get; set; }
    //}

    // 匯出 PPT 報告
    //public ActionResult ExportPpt()
    //{
    //    string fileName = "多方案光罩交期影響比較.pptx";
    //    string filePath = Server.MapPath("~/App_Data/" + fileName);

    //    using (var presentationDoc = PresentationDocument.Create(filePath, DocumentFormat.OpenXml.PresentationDocumentType.Presentation))
    //    {
    //        var presentationPart = presentationDoc.AddPresentationPart();
    //        presentationPart.Presentation = new Presentation();
    //        var slidePart = presentationPart.AddNewPart<SlidePart>();
    //        slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));

    //        var shapes = slidePart.Slide.CommonSlideData.ShapeTree;
    //        shapes.Append(CreateTextShape("多方案光罩交期影響比較", 100, 50, 600, 50));
    //        shapes.Append(CreateTextShape("方案A: 目標光罩準時，其餘延遲", 100, 150, 600, 40));
    //        shapes.Append(CreateTextShape("方案B: 全部延後一天", 100, 200, 600, 40));
    //        shapes.Append(CreateTextShape("方案C: 全部準時，額外 OPC 成本 1111", 100, 250, 600, 40));

    //        slidePart.Slide.Save();
    //        var slideIdList = presentationPart.Presentation.AppendChild(new SlideIdList());
    //        uint slideId = 256;
    //        slideIdList.Append(new SlideId { Id = slideId, RelationshipId = presentationPart.GetIdOfPart(slidePart) });
    //        presentationPart.Presentation.Save();
    //    }

    //    return File(filePath, "application/vnd.openxmlformats-officedocument.presentationml.presentation", fileName);
    //}

    //private Shape CreateTextShape(string text, int x, int y, int cx, int cy)
    //{
    //    var shape = new Shape();
    //    var nvSpPr = new NonVisualShapeProperties(new NonVisualDrawingProperties { Id = (UInt32Value)1U, Name = "TextBox" },
    //                                              new NonVisualShapeDrawingProperties(),
    //                                              new ApplicationNonVisualDrawingProperties());
    //    var spPr = new ShapeProperties(new A.Transform2D(new A.Offset { X = x * 9525, Y = y * 9525 },
    //                                                    new A.Extents { Cx = cx * 9525, Cy = cy * 9525 }));
    //    var txBody = new TextBody(new A.BodyProperties(),
    //                              new A.ListStyle(),
    //                              new A.Paragraph(new A.Run(new A.Text { Text = text })));
    //    shape.Append(nvSpPr, spPr, txBody);
    //    return shape;
    //}
    //public class HtmlRequest { public string HtmlContent { get; set; } }
    //// 這就是接收前端 AJAX 傳來的 JSON 物件
    //// 前端 AJAX 傳來的請求物件
    ////public class PptRequest
    ////{
    ////    public string A { get; set; }
    ////    public string B { get; set; }
    ////    public string C { get; set; }
    ////    public string Conclusion { get; set; }
    ////    public string Title { get; set; } // 新增：標題字串
    ////}

    //[HttpPost]
    //public ActionResult ExportPptWithLayout(PptRequest req)
    //{
    //    using (var ms = new MemoryStream())
    //    {
    //        using (var doc = PresentationDocument.Create(ms, PresentationDocumentType.Presentation))
    //        {
    //            var presentationPart = doc.AddPresentationPart();
    //            presentationPart.Presentation = new Presentation(
    //                new SlideMasterIdList(),
    //                new SlideIdList(),
    //                new SlideSize() { Cx = 9144000, Cy = 6858000, Type = SlideSizeValues.Screen4x3 },
    //                new NotesSize() { Cx = 6858000, Cy = 9144000 }
    //            );

    //            // === Slide Master ===
    //            var slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
    //            slideMasterPart.SlideMaster = new SlideMaster(new CommonSlideData(new ShapeTree()));

    //            // === Layout ===
    //            var layoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>("rId2");
    //            layoutPart.SlideLayout = new SlideLayout(new CommonSlideData(new ShapeTree()));
    //            slideMasterPart.SlideMaster.Append(new SlideLayoutIdList(
    //                new SlideLayoutId() { Id = 1U, RelationshipId = slideMasterPart.GetIdOfPart(layoutPart) }
    //            ));

    //            presentationPart.Presentation.SlideMasterIdList.Append(
    //                new SlideMasterId() { Id = 1U, RelationshipId = presentationPart.GetIdOfPart(slideMasterPart) }
    //            );

    //            // === Slide ===
    //            var slidePart = presentationPart.AddNewPart<SlidePart>("rId3");
    //            slidePart.Slide = new Slide(
    //                new CommonSlideData(
    //                    new ShapeTree(
    //                        new NonVisualGroupShapeProperties(
    //                            new NonVisualDrawingProperties() { Id = 1U, Name = "" },
    //                            new NonVisualGroupShapeDrawingProperties(),
    //                            new ApplicationNonVisualDrawingProperties()
    //                        ),
    //                        new GroupShapeProperties(new A.TransformGroup())
    //                    )
    //                )
    //            );
    //            slidePart.AddPart(layoutPart);

    //            var shapeTree = slidePart.Slide.CommonSlideData.ShapeTree;

    //            // === 標題 ===
    //            string titleText = string.IsNullOrEmpty(req.Title) ? "多方案光罩交期影響比較" : req.Title;
    //            var titleShape = new Shape(
    //                new NonVisualShapeProperties(
    //                    new NonVisualDrawingProperties() { Id = 100U, Name = "TitleBox" },
    //                    new NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
    //                    new ApplicationNonVisualDrawingProperties()),
    //                new ShapeProperties(),
    //                new TextBody(
    //                    new A.BodyProperties(),
    //                    new A.ListStyle(),
    //                    new A.Paragraph(
    //                        new A.Run(
    //                            new A.RunProperties() { Language = "zh-TW", FontSize = 3200, Bold = true },
    //                            new A.Text(titleText)
    //                        )
    //                    )
    //                )
    //            );

    //            titleShape.ShapeProperties = new ShapeProperties(
    //                new A.Transform2D(
    //                    new A.Offset() { X = 0, Y = 200000 },
    //                    new A.Extents() { Cx = 9144000, Cy = 800000 })
    //            );
    //            shapeTree.Append(titleShape);

    //            // === 工具方法：加圖片 ===
    //            Action<string, long, long, long, long> addImage = (base64, x, y, cx, cy) =>
    //            {
    //                if (string.IsNullOrEmpty(base64)) return;
    //                byte[] bytes = Convert.FromBase64String(base64.Split(',')[1]);

    //                var imgPart = slidePart.AddImagePart(ImagePartType.Png);
    //                using (var s = new MemoryStream(bytes))
    //                    imgPart.FeedData(s);

    //                string relId = slidePart.GetIdOfPart(imgPart);

    //                var pic = new Picture(
    //                    new NonVisualPictureProperties(
    //                        new NonVisualDrawingProperties() { Id = (UInt32)(shapeTree.ChildElements.Count + 1), Name = "Picture" },
    //                        new NonVisualPictureDrawingProperties(new A.PictureLocks() { NoChangeAspect = true }),
    //                        new ApplicationNonVisualDrawingProperties()),
    //                    new BlipFill(new A.Blip() { Embed = relId }, new A.Stretch(new A.FillRectangle())),
    //                    new ShapeProperties(
    //                        new A.Transform2D(
    //                            new A.Offset() { X = x, Y = y },
    //                            new A.Extents() { Cx = cx, Cy = cy })
    //                    )
    //                );

    //                shapeTree.Append(pic);
    //            };

    //            // === 插入三張方案圖 (橫向) ===
    //            addImage(req.A, 500000, 1200000, 2500000, 1500000);
    //            addImage(req.B, 3250000, 1200000, 2500000, 1500000);
    //            addImage(req.C, 6000000, 1200000, 2500000, 1500000);

    //            // === 插入結論圖 (下方) ===
    //            addImage(req.Conclusion, 500000, 3200000, 8000000, 2500000);

    //            // === SlideIdList ===
    //            presentationPart.Presentation.SlideIdList.Append(new SlideId()
    //            {
    //                Id = 256U,
    //                RelationshipId = presentationPart.GetIdOfPart(slidePart)
    //            });

    //            presentationPart.Presentation.Save();
    //        }

    //        return File(ms.ToArray(),
    //            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    //            "大師秘笈_光罩交期比較.pptx");
    //    }
    //}
    ////#region test
    ////using (MemoryStream ms = new MemoryStream())
    ////{
    ////    using (PresentationDocument presDoc =
    ////        PresentationDocument.Create(ms, PresentationDocumentType.Presentation, true))
    ////    {
    ////        PresentationPart presentationPart = presDoc.AddPresentationPart();
    ////        presentationPart.Presentation = new Presentation();

    ////        // === 設定投影片大小 (4:3) ===
    ////        presentationPart.Presentation.SlideSize = new SlideSize()
    ////        {
    ////            Cx = 9144000, // 10 in
    ////            Cy = 6858000, // 7.5 in
    ////            Type = SlideSizeValues.Screen4x3
    ////        };

    ////        // 建立 Slide
    ////        SlideMasterPart slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>();
    ////        slideMasterPart.SlideMaster = new SlideMaster(new CommonSlideData(new ShapeTree()));
    ////        SlideLayoutPart slideLayoutPart = slideMasterPart.AddNewPart<SlideLayoutPart>();
    ////        slideLayoutPart.SlideLayout = new SlideLayout(new CommonSlideData(new ShapeTree()));

    ////        SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
    ////        slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));
    ////        slidePart.AddPart(slideLayoutPart);

    ////        var tree = slidePart.Slide.CommonSlideData.ShapeTree;

    ////        // 工具方法：插入圖片 (相對比例)
    ////        Action<string, double, double, double, double> addImage = (base64, relX, relY, relW, relH) =>
    ////        {
    ////            if (string.IsNullOrEmpty(base64)) return;

    ////            byte[] bytes = Convert.FromBase64String(base64.Split(',')[1]);
    ////            ImagePart imagePart = slidePart.AddImagePart(ImagePartType.Png);
    ////            using (MemoryStream imgStream = new MemoryStream(bytes))
    ////                imagePart.FeedData(imgStream);

    ////            long slideWidth = 9144000;  // 10 in
    ////            long slideHeight = 6858000; // 7.5 in

    ////            long cx = (long)(slideWidth * relW);
    ////            long cy = (long)(slideHeight * relH);
    ////            long x = (long)(slideWidth * relX - cx / 2);
    ////            long y = (long)(slideHeight * relY - cy / 2);

    ////            var pic = new Picture(
    ////                new NonVisualPictureProperties(
    ////                    new NonVisualDrawingProperties() { Id = (UInt32)tree.ChildElements.Count + 1, Name = "Img" },
    ////                    new NonVisualPictureDrawingProperties(new A.PictureLocks() { NoChangeAspect = true }),
    ////                    new ApplicationNonVisualDrawingProperties()),
    ////                new BlipFill(
    ////                    new A.Blip() { Embed = slidePart.GetIdOfPart(imagePart) },
    ////                    new A.Stretch(new A.FillRectangle())),
    ////                new ShapeProperties(
    ////                    new A.Transform2D(
    ////                        new A.Offset() { X = x, Y = y },
    ////                        new A.Extents() { Cx = cx, Cy = cy })
    ////                )
    ////            );
    ////            tree.Append(pic);
    ////        };

    ////        // 工具方法：新增標題文字
    ////        Action<string> addTitle = (titleText) =>
    ////        {
    ////            var shapeTree = slidePart.Slide.CommonSlideData.ShapeTree;

    ////            var shape = new Shape(
    ////                new NonVisualShapeProperties(
    ////                    new NonVisualDrawingProperties() { Id = 200U, Name = "TitleBox" },
    ////                    new NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
    ////                    new ApplicationNonVisualDrawingProperties()),
    ////                new ShapeProperties(),
    ////                new TextBody(
    ////                    new A.BodyProperties(),
    ////                    new A.ListStyle(),
    ////                    new A.Paragraph(
    ////                        new A.Run(
    ////                            new A.RunProperties() { Language = "zh-TW", FontSize = 3200, Bold = true }, // 32pt
    ////                            new A.Text(titleText)
    ////                        ),
    ////                        new A.ParagraphProperties() { Alignment = A.TextAlignmentTypeValues.Center }
    ////                    )
    ////                )
    ////            );

    ////            long slideWidth = 9144000;
    ////            long cx = slideWidth;
    ////            long cy = 800000;  // 高度
    ////            long x = 0;
    ////            long y = 200000;   // 距離上方

    ////            shape.ShapeProperties = new ShapeProperties(
    ////                new A.Transform2D(
    ////                    new A.Offset() { X = x, Y = y },
    ////                    new A.Extents() { Cx = cx, Cy = cy })
    ////            );

    ////            shapeTree.Append(shape);
    ////        };

    ////        // === 插入標題 ===
    ////        addTitle(string.IsNullOrEmpty(req.Title)
    ////            ? "多方案光罩交期影響比較"
    ////            : req.Title);

    ////        // === 上半部三張圖 (高度縮小 30%) ===
    ////        // 原本 relH = 0.4 → 縮小到 0.28
    ////        addImage(req.A, 0.17, 0.38, 0.28, 0.28); // 左
    ////        addImage(req.B, 0.50, 0.38, 0.28, 0.28); // 中
    ////        addImage(req.C, 0.83, 0.38, 0.28, 0.28); // 右

    ////        // === 下半部結論圖 ===
    ////        addImage(req.Conclusion, 0.5, 0.78, 0.9, 0.3);

    ////        // 加入 SlideId
    ////        presentationPart.Presentation.SlideIdList = new SlideIdList(
    ////            new SlideId() { Id = 256U, RelationshipId = presentationPart.GetIdOfPart(slidePart) }
    ////        );
    ////        presentationPart.Presentation.Save();
    ////    }

    ////    return File(ms.ToArray(),
    ////        "application/vnd.openxmlformats-officedocument.presentationml.presentation",
    ////        "大師秘笈_光罩交期比較.pptx");
    ////}
    //////#endregion

}