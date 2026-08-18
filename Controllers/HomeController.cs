using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPlanlama.Models;

namespace UretimPlanlama.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly Data.ApplicationDbContext _context;

    public HomeController(Data.ApplicationDbContext context)
    {
        _context = context;
    }

    public class WorkshopFabricSummary
    {
        public string? WorkshopName { get; set; }
        public decimal TotalTarget { get; set; }
        public decimal TotalActual { get; set; }
        public decimal MatchRate => TotalTarget > 0 ? (TotalActual / TotalTarget) * 100 : 100;
    }

    public class WorkshopCapacityStatus
    {
        public Workshop Workshop { get; set; } = null!;
        public int DailyUsage { get; set; }
        public int MonthlyUsage { get; set; }
        public int AnnualUsage { get; set; }
        public double DailyOccupancyRate { get; set; }
        public double MonthlyOccupancyRate { get; set; }
        public double AnnualOccupancyRate { get; set; }
        public string StatusLabel { get; set; } = string.Empty;
        public string StatusClass { get; set; } = string.Empty;
        public int ActiveOrderCount { get; set; }
    }

    public IActionResult Index()
    {
        var orders = _context.Orders.Where(o => o.ModelName != "Test Model").OrderByDescending(o => o.OrderDate).ToList();
        var workshops = _context.Workshops.ToList();

        // Yeni Dashboard İstatistikleri ve Aşama Dağılımı
        int waitingQty = 0;
        int cuttingQty = 0;
        int completedQty = 0;

        int stageYeniKayit = 0;
        int stageKesim = 0;
        int stageDikim = 0;
        int stagePaket = 0;

        foreach (var o in orders)
        {
            var pDict = new Dictionary<string, string>();
            if (!string.IsNullOrEmpty(o.ProductionJson)) { try { pDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(o.ProductionJson); } catch {} }

            bool HasP(string key) => pDict != null && pDict.ContainsKey(key) && !string.IsNullOrEmpty(pDict[key]);

            bool isPaketBitis = o.PackagingEndDate.HasValue || HasP("prod_paket_bitis_actual");
            bool isPaket = o.PackagingStartDate.HasValue || HasP("prod_paket_baslangic_actual") || isPaketBitis;
            bool isDikimBitis = o.SewingEndDate.HasValue || HasP("prod_dikim_bitis_actual");
            bool isDikim = o.SewingStartDate.HasValue || HasP("prod_dikim_baslangic_actual") || isDikimBitis || isPaket;
            
            bool hasCuttingData = !string.IsNullOrEmpty(o.CuttingProcessJson) && o.CuttingProcessJson != "[]" && o.CuttingProcessJson != "null";
            bool isKesimBitis = o.CuttingEndDate.HasValue || HasP("prod_kesim_bitis_actual") || isDikim;
            bool isKesim = o.CuttingStartDate.HasValue || HasP("prod_kesim_baslangic_actual") || hasCuttingData || isKesimBitis;

            // Kesim KPI Hesaplamaları
            if (isKesimBitis) {
                completedQty += o.CalculatedQuantity;
            } else if (isKesim) {
                cuttingQty += o.CalculatedQuantity;
            } else {
                waitingQty += o.CalculatedQuantity;
            }

            // Pasta Grafik Dağılımı
            if (isPaket) {
                stagePaket++;
            } else if (isDikim) {
                stageDikim++;
            } else if (isKesim) {
                stageKesim++;
            } else {
                stageYeniKayit++;
            }
        }

        ViewBag.TotalOrdersQty = orders.Sum(o => o.CalculatedQuantity);
        ViewBag.WaitingQty = waitingQty;
        ViewBag.CuttingQty = cuttingQty;
        ViewBag.CompletedQty = completedQty;

        // Kritik Siparişler (Gecikenler)
        var criticalOrders = orders.Where(o => o.EffectiveTerminDate < DateTime.Today && o.Status != "Tamamlandı" && o.Status != "İptal Edildi").OrderBy(o => o.EffectiveTerminDate).ToList();
        ViewBag.CriticalOrders = criticalOrders;

        // Yaklaşan Terminler (Önümüzdeki 15 gün)
        var upcomingDeadlines = orders.Where(o => o.EffectiveTerminDate >= DateTime.Today && o.EffectiveTerminDate <= DateTime.Today.AddDays(15) && o.Status != "Tamamlandı" && o.Status != "İptal Edildi").OrderBy(o => o.EffectiveTerminDate).ToList();
        ViewBag.UpcomingDeadlines = upcomingDeadlines;

        ViewBag.StageDistribution = new[] { stageYeniKayit, stageKesim, stageDikim, stagePaket };

        // Son Hareketler (Bildirimler)
        ViewBag.RecentNotifications = _context.Notifications.OrderByDescending(n => n.CreatedAt).Take(8).ToList();
        // Atölye bazlı Kumaş Karşılaştırma Takibi
        var workshopSummaries = orders
            .Where(o => !string.IsNullOrEmpty(o.ProductionPlace))
            .GroupBy(o => o.ProductionPlace)
            .Select(g => new WorkshopFabricSummary
            {
                WorkshopName = g.Key,
                TotalTarget = g.Sum(o => o.TargetFabricQty ?? 0),
                TotalActual = g.Sum(o => o.ActualFabricQty ?? 0)
            })
            .ToList();

        ViewBag.WorkshopSummaries = workshopSummaries;

        // Atölye Performans / Termin Başarısı
        var workshopPerformanceList = new List<object>();
        foreach (var w in workshops) {
            int onTime = 0;
            int delayed = 0;
            
            var wsCompletedOrders = orders.Where(o => 
                (o.SewingWorkshop == w.Name || o.ProductionPlace == w.Name) && 
                (o.Status == "Tamamlandı" || o.PackagingEndDate.HasValue || (o.ProductionJson != null && (o.ProductionJson.Contains("prod_depo_varis_actual") || o.ProductionJson.Contains("prod_paket_bitis_actual"))))
            ).ToList();
            
            foreach(var o in wsCompletedOrders) {
                DateTime? actualEnd = null;
                if (!string.IsNullOrEmpty(o.ProductionJson)) {
                    try {
                        var pDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(o.ProductionJson);
                        if (pDict != null) {
                            // Öncelikli olarak Depo Varış tarihini (prod_depo_varis_actual) baz al
                            if (pDict.TryGetValue("prod_depo_varis_actual", out var depoStr) && DateTime.TryParse(depoStr, out DateTime depoDate)) {
                                actualEnd = depoDate;
                            } 
                            // Eğer depo varış girilmediyse Paket Bitiş tarihini kullan
                            else if (pDict.TryGetValue("prod_paket_bitis_actual", out var pktStr) && DateTime.TryParse(pktStr, out DateTime pktDate)) {
                                actualEnd = pktDate;
                            }
                        }
                    } catch {}
                }
                
                // CPS formu kullanılmamışsa ve eski sistem (PackagingEndDate) kullanıldıysa
                if (!actualEnd.HasValue) {
                    actualEnd = o.PackagingEndDate;
                }
                
                DateTime? planEnd = o.EffectiveTerminDate;
                if (actualEnd.HasValue && planEnd.HasValue) {
                    if (actualEnd.Value.Date <= planEnd.Value.Date) onTime++;
                    else delayed++;
                }
            }
            
            if (onTime > 0 || delayed > 0) {
                workshopPerformanceList.Add(new {
                    Workshop = w.Name,
                    OnTime = onTime,
                    Delayed = delayed,
                    SuccessRate = (int)Math.Round((double)onTime / (onTime + delayed) * 100)
                });
            }
        }
        ViewBag.WorkshopPerformanceJson = System.Text.Json.JsonSerializer.Serialize(workshopPerformanceList);

        // Atölye bazlı Kapasite ve Doluluk Takibi
        var today = DateTime.Today;
        var currentMonth = today.Month;
        var currentYear = today.Year;

        var capacityStatuses = new List<WorkshopCapacityStatus>();
        foreach (var w in workshops)
        {
            var wOrders = orders
                .Where(o => {
                    if (o.Status == "İptal Edildi") return false;
                    if (o.SewingWorkshop == w.Name || o.ProductionPlace == w.Name) return true;
                    if (!string.IsNullOrEmpty(o.ProductionJson)) {
                        try {
                            var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(o.ProductionJson);
                            if (dict != null && dict.TryGetValue("prod_dikim_atolyesi", out var dikim) && dikim == w.Name) return true;
                        } catch {}
                    }
                    return false;
                })
                .ToList();

            int dailyUsage = 0;
            int monthlyUsage = 0;
            int annualUsage = 0;

            foreach (var o in wOrders)
            {
                DateTime refDate = o.SewingStartDate ?? o.PlannedSewingStartDate ?? o.OrderDate;
                int qty = o.CalculatedQuantity > 0 ? o.CalculatedQuantity : o.Quantity;
                
                if (!string.IsNullOrEmpty(o.ProductionJson) && o.ProductionJson.Contains("\"prod_dikim_miktari\""))
                {
                    try {
                        var pDict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(o.ProductionJson);
                        if (pDict != null && pDict.ContainsKey("prod_dikim_miktari") && int.TryParse(pDict["prod_dikim_miktari"], out int pdm)) {
                            qty = pdm;
                        }
                    } catch {}
                }

                if (refDate.Date == today) dailyUsage += qty;
                if (refDate.Year == currentYear && refDate.Month == currentMonth) monthlyUsage += qty;
                if (refDate.Year == currentYear) annualUsage += qty;
            }

            var dailyRate = w.DailyCapacity > 0 ? ((double)dailyUsage / w.DailyCapacity) * 100 : 0;
            var monthlyRate = w.MonthlyCapacity > 0 ? ((double)monthlyUsage / w.MonthlyCapacity) * 100 : 0;
            var annualRate = w.AnnualCapacity > 0 ? ((double)annualUsage / w.AnnualCapacity) * 100 : 0;

            // En kritik doluluk oranına göre durum belirle
            var primaryRate = w.MonthlyCapacity > 0 ? monthlyRate : (w.DailyCapacity > 0 ? dailyRate : 0);
            
            string statusLabel = "Boş / Müsait";
            string statusClass = "badge-progress"; // Yeşil
            
            if (primaryRate >= 100)
            {
                statusLabel = "Kapasite Dolu";
                statusClass = "badge-high"; // Kırmızı
            }
            else if (primaryRate >= 75)
            {
                statusLabel = "Yoğun Çalışıyor";
                statusClass = "badge-medium"; // Sarı
            }

            capacityStatuses.Add(new WorkshopCapacityStatus
            {
                Workshop = w,
                DailyUsage = dailyUsage,
                MonthlyUsage = monthlyUsage,
                AnnualUsage = annualUsage,
                DailyOccupancyRate = Math.Round(dailyRate, 1),
                MonthlyOccupancyRate = Math.Round(monthlyRate, 1),
                AnnualOccupancyRate = Math.Round(annualRate, 1),
                StatusLabel = statusLabel,
                StatusClass = statusClass,
                ActiveOrderCount = wOrders.Count
            });
        }
        // Sadece üzerinde siparişi olan atölyeleri göster
        ViewBag.WorkshopCapacities = capacityStatuses
            .Where(c => c.ActiveOrderCount > 0)
            .OrderByDescending(c => c.MonthlyOccupancyRate)
            .ToList();

        // Tüm atölyelerin kapasite durumlarını Kanban panosunda bar çizmek için aktar
        ViewBag.AllWorkshopCapacities = capacityStatuses.ToDictionary(c => c.Workshop.Name, c => c);

        ViewBag.AllActiveWorkshops = workshops.Where(w => w.IsActive).ToList();

        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> ClearNotifications()
    {
        try
        {
            _context.Notifications.RemoveRange(_context.Notifications);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (System.Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    public class AssignWorkshopModel {
        public int OrderId { get; set; }
        public string WorkshopName { get; set; }
        public bool ForceAssign { get; set; }
        public bool ForceShift { get; set; }
        public DateTime? SuggestedStartDate { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> AssignWorkshop([FromBody] AssignWorkshopModel model)
    {
        try
        {
            var order = await _context.Orders.FindAsync(model.OrderId);
            if (order == null) return Json(new { success = false, message = "Sipariş bulunamadı." });

            if (string.IsNullOrEmpty(model.WorkshopName))
            {
                order.SewingWorkshop = null;
                order.ProductionPlace = null;
                _context.Update(order);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            var workshop = await _context.Workshops.FirstOrDefaultAsync(w => w.Name == model.WorkshopName);
            if (workshop == null) return Json(new { success = false, message = "Atölye bulunamadı." });

            if (!model.ForceAssign && !model.ForceShift)
            {
                int qty = order.CalculatedQuantity > 0 ? order.CalculatedQuantity : order.Quantity;
                DateTime refDate = order.PlannedSewingStartDate ?? order.OrderDate;

                if (workshop.DailyCapacity > 0)
                {
                    // Fetch relevant orders to calculate load (CalculatedQuantity is NotMapped)
                    var relevantOrders = await _context.Orders
                        .Where(o => o.Id != order.Id && 
                                    (o.SewingWorkshop == workshop.Name || o.ProductionPlace == workshop.Name) &&
                                    o.Status != "İptal Edildi")
                        .ToListAsync();

                    int loadOnRefDate = relevantOrders
                        .Where(o => (o.PlannedSewingStartDate ?? o.OrderDate).Date == refDate.Date)
                        .Sum(o => o.CalculatedQuantity > 0 ? o.CalculatedQuantity : o.Quantity);

                    if (loadOnRefDate + qty > workshop.DailyCapacity)
                    {
                        DateTime? nextAvailableDate = null;
                        for (int i = 1; i <= 30; i++)
                        {
                            DateTime checkDate = refDate.AddDays(i);
                            int load = relevantOrders
                                .Where(o => (o.PlannedSewingStartDate ?? o.OrderDate).Date == checkDate.Date)
                                .Sum(o => o.CalculatedQuantity > 0 ? o.CalculatedQuantity : o.Quantity);

                            if (load + qty <= workshop.DailyCapacity)
                            {
                                nextAvailableDate = checkDate;
                                break;
                            }
                        }

                        if (nextAvailableDate.HasValue)
                        {
                            return Json(new { 
                                success = false, 
                                requiresConfirmation = true, 
                                suggestedStartDate = nextAvailableDate.Value.ToString("yyyy-MM-dd"), 
                                message = $"Seçilen atölyenin {refDate:dd.MM.yyyy} tarihindeki günlük kapasitesi doludur. Siparişin planlanan dikim tarihini {nextAvailableDate.Value:dd.MM.yyyy} olarak kaydırmak ister misiniz?" 
                            });
                        }
                        else
                        {
                            return Json(new { 
                                success = false, 
                                requiresConfirmation = true, 
                                suggestedStartDate = (DateTime?)null, 
                                message = $"Seçilen atölyenin {refDate:dd.MM.yyyy} tarihindeki kapasitesi doludur ve önümüzdeki 30 gün içinde boşluk bulunamadı. Kapasiteyi aşarak atamak ister misiniz?" 
                            });
                        }
                    }
                }
            }

            if (model.ForceShift && model.SuggestedStartDate.HasValue)
            {
                TimeSpan? duration = null;
                if (order.PlannedSewingStartDate.HasValue && order.PlannedSewingEndDate.HasValue)
                {
                    duration = order.PlannedSewingEndDate.Value - order.PlannedSewingStartDate.Value;
                }
                
                order.PlannedSewingStartDate = model.SuggestedStartDate;
                
                if (duration.HasValue)
                {
                    order.PlannedSewingEndDate = model.SuggestedStartDate.Value.Add(duration.Value);
                }
            }

            order.SewingWorkshop = model.WorkshopName;
            order.ProductionPlace = model.WorkshopName; // Ana üretim yeri olarak da işaretle
            
            _context.Update(order);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
        catch (System.Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ResetDatabase()
    {
        try
        {
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM OrderMaterials");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StokHareketler");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM CariHareketler");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Orders");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StokVaryantlar");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM StokKartlari");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM CariHesaplar");
            await _context.Database.ExecuteSqlRawAsync("DELETE FROM Notifications");
            
            return Json(new { success = true, message = "Siparişler, stok kayıtları, cari hareketler ve bildirimler başarıyla temizlendi. (Tanımlamalar korundu)" });
        }
        catch (System.Exception ex)
        {
            return Json(new { success = false, message = "Hata oluştu: " + ex.Message });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
