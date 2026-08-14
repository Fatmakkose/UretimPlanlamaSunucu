using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UretimPlanlama.Data;
using UretimPlanlama.Models;
using ClosedXML.Excel;

namespace UretimPlanlama.Controllers
{
    [Authorize(Policy = "DepoAccess")]
    public class StockController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StockController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            if (!User.HasPermission("View"))
            {
                return RedirectToAction("AccessDenied", "Account");
            }
            var stoklar = _context.StokKartlari.OrderByDescending(s => s.OlusturmaTarihi).ToList();
            ViewBag.Orders = _context.Orders.OrderByDescending(o => o.OrderDate).ToList();
            ViewBag.KritikStokSayisi = stoklar.Count(s => s.Aktif && s.MevcutMiktar <= s.MinimumMiktar && s.MinimumMiktar > 0);
            return View(stoklar);
        }

        public IActionResult Definitions()
        {
            return RedirectToAction("Index");
        }

        public IActionResult Movements()
        {
            if (!User.HasPermission("View"))
            {
                return RedirectToAction("AccessDenied", "Account");
            }
            var hareketler = _context.StokHareketler
                .Include(h => h.StokKarti)
                .Include(h => h.StokVaryant)
                .OrderByDescending(h => h.IslemTarihi)
                .ThenByDescending(h => h.Id)
                .ToList();
            return View(hareketler);
        }

        public IActionResult Warehouse()
        {
            if (!User.HasPermission("View"))
            {
                return RedirectToAction("AccessDenied", "Account");
            }
            
            var stoklar = _context.StokKartlari.ToList();
            ViewBag.KritikStokSayisi = stoklar.Count(s => s.Aktif && s.MevcutMiktar <= s.MinimumMiktar && s.MinimumMiktar > 0);
            ViewBag.StokKartlari = stoklar;
            
            // Siparişleri getir
            var orders = _context.Orders
                .Where(o => o.Status != "İptal")
                .OrderByDescending(o => o.OrderDate)
                .ToList();
                
            return View(orders);
        }

        [HttpGet]
        public IActionResult GetVaryantlar(int stokKartiId)
        {
            var varyantlar = _context.StokVaryantlar
                .Where(v => v.StokKartiId == stokKartiId && v.Aktif)
                .Select(v => new { v.Id, v.VaryantAdi, v.MevcutMiktar })
                .ToList();
            return Json(new { success = true, data = varyantlar });
        }

        [HttpGet]
        public IActionResult GetStokDetail(int id)
        {
            var stok = _context.StokKartlari.Find(id);
            if (stok == null)
                return Json(new { success = false, message = "Stok kartı bulunamadı." });

            var hareketler = _context.StokHareketler
                .Where(h => h.StokKartiId == id && !(h.HareketTipi == "Transfer" && h.Miktar < 0))
                .OrderByDescending(h => h.IslemTarihi)
                .Select(h => new
                {
                    h.Id,
                    IslemTarihi = h.IslemTarihi.ToString("dd.MM.yyyy"),
                    h.HareketTipi,
                    h.Aciklama,
                    h.Miktar,
                    h.KalanMiktar,
                    h.BelgeNo,
                    h.OrderId
                })
                .ToList();

            return Json(new { success = true, stok = stok, hareketler = hareketler });
        }

        [HttpPost]
        public IActionResult CreateStokKarti([FromForm] StokKarti model, IFormFile? gorselDosya)
        {
            if (!User.HasPermission("Write"))
                return Json(new { success = false, message = "Yetkiniz yetersiz." });

            if (string.IsNullOrEmpty(model.StokAdi))
                return Json(new { success = false, message = "Stok adı zorunludur." });

            try
            {
                // Otomatik stok kodu oluştur
                if (string.IsNullOrEmpty(model.StokKodu))
                {
                    var prefix = model.Kategori switch
                    {
                        "Kumaş" => "KMS",
                        "Malzeme" => "MLZ",
                        "Tela" => "TLA",
                        "Etiket" => "ETK",
                        _ => "STK"
                    };
                    var lastCode = _context.StokKartlari
                        .Where(s => s.StokKodu.StartsWith(prefix + "-"))
                        .OrderByDescending(s => s.StokKodu)
                        .Select(s => s.StokKodu)
                        .FirstOrDefault();

                    int nextNum = 1;
                    if (!string.IsNullOrEmpty(lastCode))
                    {
                        var numPart = lastCode.Replace(prefix + "-", "");
                        int.TryParse(numPart, out nextNum);
                        nextNum++;
                    }
                    model.StokKodu = $"{prefix}-{nextNum:D4}";
                }

                model.OlusturmaTarihi = DateTime.Now;
                model.MevcutMiktar = 0;

                if (gorselDosya != null && gorselDosya.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "stok");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + gorselDosya.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        gorselDosya.CopyTo(fileStream);
                    }
                    model.GorselUrl = "/uploads/stok/" + uniqueFileName;
                }

                _context.StokKartlari.Add(model);
                _context.SaveChanges();
                return Json(new { success = true, message = "Stok kartı başarıyla oluşturuldu.", stok = model });
            }
            catch (Exception ex)
            {
                var msg = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = msg });
            }
        }

        [HttpPost]
        public IActionResult EditStokKarti([FromForm] StokKarti model, IFormFile? gorselDosya)
        {
            if (!User.HasPermission("Write"))
                return Json(new { success = false, message = "Yetkiniz yetersiz." });

            try
            {
                var existing = _context.StokKartlari.Find(model.Id);
                if (existing == null)
                    return Json(new { success = false, message = "Stok kartı bulunamadı." });

                if (existing.Kategori != model.Kategori)
                {
                    var prefix = model.Kategori switch
                    {
                        "Kumaş" => "KMS",
                        "Malzeme" => "MLZ",
                        "Tela" => "TLA",
                        "Etiket" => "ETK",
                        _ => "STK"
                    };
                    var lastCode = _context.StokKartlari
                        .Where(s => s.StokKodu.StartsWith(prefix + "-"))
                        .OrderByDescending(s => s.StokKodu)
                        .Select(s => s.StokKodu)
                        .FirstOrDefault();

                    int nextNum = 1;
                    if (!string.IsNullOrEmpty(lastCode))
                    {
                        var numPart = lastCode.Replace(prefix + "-", "");
                        int.TryParse(numPart, out nextNum);
                        nextNum++;
                    }
                    existing.StokKodu = $"{prefix}-{nextNum:D4}";
                }

                existing.StokAdi = model.StokAdi;
                existing.Kategori = model.Kategori;
                existing.Birim = model.Birim;
                existing.MinimumMiktar = model.MinimumMiktar;
                existing.Aktif = model.Aktif;
                existing.OzelliklerJson = model.OzelliklerJson;
                
                if (gorselDosya != null && gorselDosya.Length > 0)
                {
                    var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "stok");
                    if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);
                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + gorselDosya.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        gorselDosya.CopyTo(fileStream);
                    }
                    existing.GorselUrl = "/uploads/stok/" + uniqueFileName;
                }
                else if (model.GorselUrl != null)
                {
                    // Form data didn't include file but passed the old url
                    existing.GorselUrl = model.GorselUrl;
                }

                _context.SaveChanges();
                return Json(new { success = true, message = "Stok kartı güncellendi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult CreateHareket([FromBody] StokHareket model)
        {
            if (!User.HasPermission("Write"))
                return Json(new { success = false, message = "Yetkiniz yetersiz." });

            try
            {
                var stok = _context.StokKartlari.Find(model.StokKartiId);
                if (stok == null)
                    return Json(new { success = false, message = "Stok kartı bulunamadı." });

                if (model.Miktar <= 0)
                    return Json(new { success = false, message = "Miktar sıfırdan büyük olmalıdır." });

                model.IslemTarihi = model.IslemTarihi == default ? DateTime.Now : model.IslemTarihi;

                var varyant = model.StokVaryantId.HasValue ? _context.StokVaryantlar.Find(model.StokVaryantId.Value) : null;

                // Stok miktarını güncelle
                switch (model.HareketTipi)
                {
                    case "Giriş":
                        stok.MevcutMiktar += model.Miktar;
                        if (varyant != null) varyant.MevcutMiktar += model.Miktar;
                        break;
                    case "Çıkış":
                    case "Fire":
                        if (stok.MevcutMiktar < model.Miktar || (varyant != null && varyant.MevcutMiktar < model.Miktar))
                            return Json(new { success = false, message = "Yetersiz stok! Mevcut: " + (varyant != null ? varyant.MevcutMiktar : stok.MevcutMiktar) + " " + stok.Birim });
                        stok.MevcutMiktar -= model.Miktar;
                        if (varyant != null) varyant.MevcutMiktar -= model.Miktar;
                        break;
                    case "Sayım Düzeltme":
                        if (varyant != null) {
                            decimal fark = model.Miktar - varyant.MevcutMiktar;
                            varyant.MevcutMiktar = model.Miktar;
                            stok.MevcutMiktar += fark; // ana stoğa aradaki farkı yansıt
                        } else {
                            stok.MevcutMiktar = model.Miktar;
                        }
                        break;
                }

                model.KalanMiktar = stok.MevcutMiktar;

                _context.StokHareketler.Add(model);
                _context.SaveChanges();

                // Kritik stok uyarısı kontrolü
                bool kritikStok = stok.MinimumMiktar > 0 && stok.MevcutMiktar <= stok.MinimumMiktar;

                return Json(new
                {
                    success = true,
                    message = "Stok hareketi kaydedildi.",
                    yeniMiktar = stok.MevcutMiktar,
                    kritikStok = kritikStok,
                    kritikMesaj = kritikStok ? $"⚠ {stok.StokAdi} stoku kritik seviyenin altında! Mevcut: {stok.MevcutMiktar} {stok.Birim}" : null
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeleteHareket(int id)
        {
            if (!User.HasPermission("Write"))
                return Json(new { success = false, message = "Yetkiniz yetersiz." });

            try
            {
                var hareket = _context.StokHareketler.Find(id);
                if (hareket == null)
                    return Json(new { success = false, message = "Hareket bulunamadı." });

                var stok = _context.StokKartlari.Find(hareket.StokKartiId);
                if (stok != null)
                {
                    var varyant = hareket.StokVaryantId.HasValue ? _context.StokVaryantlar.Find(hareket.StokVaryantId.Value) : null;
                    // Hareketi geri al
                    switch (hareket.HareketTipi)
                    {
                        case "Giriş":
                            stok.MevcutMiktar -= hareket.Miktar;
                            if (varyant != null) varyant.MevcutMiktar -= hareket.Miktar;
                            break;
                        case "Çıkış":
                        case "Fire":
                            stok.MevcutMiktar += hareket.Miktar;
                            if (varyant != null) varyant.MevcutMiktar += hareket.Miktar;
                            break;
                    }
                }

                _context.StokHareketler.Remove(hareket);
                _context.SaveChanges();
                return Json(new { success = true, message = "Hareket silindi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult DeleteStokKarti(int id)
        {
            if (!User.HasPermission("Write"))
                return Json(new { success = false, message = "Yetkiniz yetersiz." });

            try
            {
                var stok = _context.StokKartlari.Include(s => s.Hareketler).FirstOrDefault(s => s.Id == id);
                if (stok == null)
                    return Json(new { success = false, message = "Stok kartı bulunamadı." });

                _context.StokKartlari.Remove(stok);
                _context.SaveChanges();
                return Json(new { success = true, message = "Stok kartı silindi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public IActionResult GetKritikStoklar()
        {
            var kritikler = _context.StokKartlari
                .Where(s => s.Aktif && s.MinimumMiktar > 0 && s.MevcutMiktar <= s.MinimumMiktar)
                .OrderBy(s => s.MevcutMiktar)
                .ToList();

            return Json(new { success = true, kritikler = kritikler });
        }

        [HttpGet]
        public IActionResult ExportToExcel()
        {
            if (!User.HasPermission("View"))
                return RedirectToAction("AccessDenied", "Account");

            var stoklar = _context.StokKartlari.OrderBy(s => s.Kategori).ThenBy(s => s.StokAdi).ToList();

            using (var workbook = new XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Stok Listesi");
                var currentRow = 1;

                worksheet.Cell(currentRow, 1).Value = "Stok Kodu";
                worksheet.Cell(currentRow, 2).Value = "Stok Adı";
                worksheet.Cell(currentRow, 3).Value = "Kategori";
                worksheet.Cell(currentRow, 4).Value = "Birim";
                worksheet.Cell(currentRow, 5).Value = "Mevcut Miktar";
                worksheet.Cell(currentRow, 6).Value = "Minimum Miktar";
                worksheet.Cell(currentRow, 7).Value = "Durum";

                var headerRange = worksheet.Range(1, 1, 1, 7);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;

                foreach (var s in stoklar)
                {
                    currentRow++;
                    worksheet.Cell(currentRow, 1).Value = s.StokKodu;
                    worksheet.Cell(currentRow, 2).Value = s.StokAdi;
                    worksheet.Cell(currentRow, 3).Value = s.Kategori;
                    worksheet.Cell(currentRow, 4).Value = s.Birim;
                    worksheet.Cell(currentRow, 5).Value = (double)s.MevcutMiktar;
                    worksheet.Cell(currentRow, 6).Value = (double)s.MinimumMiktar;
                    worksheet.Cell(currentRow, 7).Value = s.MevcutMiktar <= s.MinimumMiktar && s.MinimumMiktar > 0 ? "KRİTİK" : "Normal";
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "StokListesi.xlsx");
                }
            }
        }

        [HttpGet]
        public IActionResult MigrateCategories()
        {
            var stoklar = _context.StokKartlari.ToList();
            int guncellenen = 0;
            foreach (var stok in stoklar)
            {
                if (stok.Kategori == "Aksesuar" || stok.Kategori == "İplik" || stok.Kategori == "Düğme" || stok.Kategori == "Diğer")
                {
                    stok.Kategori = "Malzeme";
                    guncellenen++;
                }
            }
            if (guncellenen > 0)
            {
                _context.SaveChanges();
            }
            return Content($"Tamamlandı. Güncellenen kayıt sayısı: {guncellenen}");
        }
        [HttpGet]
        public IActionResult FixStocks()
        {
            var stoklar = _context.StokKartlari.ToList();
            foreach (var s in stoklar)
            {
                var hareketler = _context.StokHareketler.Where(h => h.StokKartiId == s.Id).OrderBy(h => h.IslemTarihi).ThenBy(h => h.Id).ToList();
                decimal total = 0;
                foreach(var h in hareketler)
                {
                    if (h.HareketTipi == "Giriş") total += h.Miktar;
                    else if (h.HareketTipi == "Çıkış" || h.HareketTipi == "Fire") total -= h.Miktar;
                    else if (h.HareketTipi == "Sayım Düzeltme") total = h.Miktar;
                }
                s.MevcutMiktar = total;

                var varyantlar = _context.StokVaryantlar.Where(v => v.StokKartiId == s.Id).ToList();
                foreach (var v in varyantlar)
                {
                    var vHareketler = hareketler.Where(h => h.StokVaryantId == v.Id).ToList();
                    decimal vTotal = 0;
                    foreach(var h in vHareketler)
                    {
                        if (h.HareketTipi == "Giriş") vTotal += h.Miktar;
                        else if (h.HareketTipi == "Çıkış" || h.HareketTipi == "Fire") vTotal -= h.Miktar;
                        else if (h.HareketTipi == "Sayım Düzeltme") vTotal = h.Miktar;
                    }
                    v.MevcutMiktar = vTotal;
                }
            }
            _context.SaveChanges();
            return Content("Tüm stok ve varyant miktarları depo hareketlerine göre yeniden hesaplandı ve senkronize edildi.");
        }
        [HttpGet]
        public IActionResult GetActiveOrders()
        {
            try
            {
                var orders = _context.Orders
                    .Where(o => o.Status != "Sevk Edildi" && o.Status != "Tamamlandı" && o.Status != "İptal")
                    .OrderByDescending(o => o.OrderDate)
                    .Select(o => new { o.Id, o.OrderCode, o.ModelName })
                    .ToList();
                return Json(new { success = true, data = orders });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private string FormatOzelliklerJson(string json)
        {
            if (string.IsNullOrEmpty(json)) return "";
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(json);
                var parts = new System.Collections.Generic.List<string>();
                if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Array) {
                    foreach(var i in doc.RootElement.EnumerateArray()) {
                        if (i.TryGetProperty("Key", out var k) && i.TryGetProperty("Value", out var v)) {
                            string keyStr = k.GetString() ?? "";
                            if (!keyStr.Contains("BİRİM", StringComparison.OrdinalIgnoreCase) && 
                                !keyStr.Contains("BIRIM", StringComparison.OrdinalIgnoreCase) && 
                                !keyStr.Contains("FİRE", StringComparison.OrdinalIgnoreCase) && 
                                !keyStr.Contains("FIRE", StringComparison.OrdinalIgnoreCase)) {
                                parts.Add($"{keyStr}: {v.GetString()}");
                            }
                        }
                    }
                } else if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object) {
                    foreach(var prop in doc.RootElement.EnumerateObject()) {
                        string keyStr = prop.Name;
                        if (!keyStr.Contains("BİRİM", StringComparison.OrdinalIgnoreCase) && 
                            !keyStr.Contains("BIRIM", StringComparison.OrdinalIgnoreCase) && 
                            !keyStr.Contains("FİRE", StringComparison.OrdinalIgnoreCase) && 
                            !keyStr.Contains("FIRE", StringComparison.OrdinalIgnoreCase)) {
                            parts.Add($"{keyStr}: {prop.Value.GetString()}");
                        }
                    }
                }
                
                if (parts.Count > 0)
                {
                    return string.Join(" | ", parts.Where(v => !string.IsNullOrEmpty(v)));
                }
                return "";
            }
            catch { }
            return json; // If it's not JSON, return as is
        }

        private string GetStokAdiWithOzellik(StokKarti stok)
        {
            if (stok == null) return "";
            var formatted = FormatOzelliklerJson(stok.OzelliklerJson);
            return string.IsNullOrEmpty(formatted) ? stok.StokAdi : $"{stok.StokAdi} - {formatted}";
        }

        [HttpGet]
        public IActionResult GetOrderPurchases(int orderId)
        {
            try
            {
                var hareketlerList = _context.StokHareketler
                    .Include(h => h.StokKarti)
                    .Include(h => h.StokVaryant)
                    .Where(h => (h.HareketTipi == "Giriş" || (h.HareketTipi == "Transfer" && h.Miktar > 0) || h.HareketTipi == "Çıkış") && h.OrderId == orderId)
                    .OrderByDescending(h => h.IslemTarihi)
                    .ToList();

                var hareketler = hareketlerList
                    .Select(h => new
                    {
                        StokAdi = h.StokKarti != null ? 
                            (h.StokVaryant != null ? $"{h.StokKarti.StokAdi} - {FormatOzelliklerJson(h.StokVaryant.VaryantAdi)}" : GetStokAdiWithOzellik(h.StokKarti))
                            : "",
                        StokKodu = h.StokKarti != null ? h.StokKarti.StokKodu : "",
                        Birim = h.StokKarti != null ? h.StokKarti.Birim : "",
                        Miktar = h.HareketTipi == "Çıkış" ? -h.Miktar : h.Miktar,
                        Tedarikci = h.HareketTipi == "Çıkış" ? "ÜRETİM (ÇIKIŞ)" : h.Tedarikci,
                        IslemTarihi = h.IslemTarihi.ToString("dd.MM.yyyy")
                    })
                    .ToList();

                return Json(new { success = true, data = hareketler });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        [HttpGet]
        public IActionResult GetOrderStocksForTransfer(int orderId)
        {
            try
            {
                var hareketler = _context.StokHareketler
                    .Include(h => h.StokKarti)
                    .Where(h => h.OrderId == orderId)
                    .ToList();
                    
                var orderStocks = hareketler.GroupBy(h => h.StokKartiId)
                    .Select(g => new {
                        StokKartiId = g.Key,
                        StokKarti = g.First().StokKarti,
                        TotalMiktar = g.Sum(h => (h.HareketTipi == "Giriş" ? h.Miktar : (h.HareketTipi == "Çıkış" || h.HareketTipi == "Fire" ? -h.Miktar : (h.HareketTipi == "Sayım Düzeltme" ? h.Miktar : (h.HareketTipi == "Transfer" ? h.Miktar : 0)))))
                    })
                    .Where(x => x.TotalMiktar > 0 && x.StokKarti != null)
                    .Select(x => new {
                        StokKartiId = x.StokKartiId,
                        StokAdi = x.StokKarti != null ? GetStokAdiWithOzellik(x.StokKarti) : "",
                        StokKodu = x.StokKarti.StokKodu,
                        Birim = x.StokKarti.Birim,
                        MevcutMiktar = x.TotalMiktar
                    })
                    .ToList();

                return Json(new { success = true, data = orderStocks });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public IActionResult ExecuteTransfer(int sourceOrderId, int targetOrderId, int stokKartiId, decimal miktar)
        {
            if (!User.HasPermission("Write"))
                return Json(new { success = false, message = "Yetkiniz yetersiz." });

            if (sourceOrderId == targetOrderId)
                return Json(new { success = false, message = "Kaynak ve hedef sipariş aynı olamaz." });
                
            if (miktar <= 0)
                return Json(new { success = false, message = "Miktar sıfırdan büyük olmalıdır." });

            using var transaction = _context.Database.BeginTransaction();
            try
            {
                var stok = _context.StokKartlari.Find(stokKartiId);
                if (stok == null) return Json(new { success = false, message = "Stok kartı bulunamadı." });

                var sourceOrder = _context.Orders.Find(sourceOrderId);
                var targetOrder = _context.Orders.Find(targetOrderId);
                if (sourceOrder == null || targetOrder == null)
                    return Json(new { success = false, message = "Sipariş bulunamadı." });

                // Calculate available stock for this order
                var hareketler = _context.StokHareketler.Where(h => h.OrderId == sourceOrderId && h.StokKartiId == stokKartiId).ToList();
                decimal available = 0;
                foreach(var h in hareketler)
                {
                    if (h.HareketTipi == "Giriş") available += h.Miktar;
                    else if (h.HareketTipi == "Çıkış" || h.HareketTipi == "Fire") available -= h.Miktar;
                    else if (h.HareketTipi == "Sayım Düzeltme") available = h.Miktar;
                    else if (h.HareketTipi == "Transfer") available += h.Miktar;
                }

                if (miktar > available)
                    return Json(new { success = false, message = $"Bu siparişte belirtilen stoktan en fazla {available} {stok.Birim} bulunmaktadır." });

                // Decrease total stock temporarily
                stok.MevcutMiktar -= miktar;
                var cikis = new StokHareket
                {
                    StokKartiId = stokKartiId,
                    IslemTarihi = DateTime.Now,
                    HareketTipi = "Transfer",
                    Miktar = -miktar,
                    KalanMiktar = stok.MevcutMiktar,
                    Aciklama = $"{sourceOrder.OrderCode} nolu siparişten {targetOrder.OrderCode} nolu siparişe transfer edildi.",
                    OrderId = sourceOrderId,
                    Tedarikci = $"{targetOrder.OrderCode} Siparişine Transfer"
                };
                _context.StokHareketler.Add(cikis);

                // Increase total stock back
                stok.MevcutMiktar += miktar;
                var giris = new StokHareket
                {
                    StokKartiId = stokKartiId,
                    IslemTarihi = DateTime.Now,
                    HareketTipi = "Transfer",
                    Miktar = miktar,
                    KalanMiktar = stok.MevcutMiktar,
                    Aciklama = $"{sourceOrder.OrderCode} nolu siparişten {targetOrder.OrderCode} nolu siparişe transfer edildi.",
                    OrderId = targetOrderId,
                    Tedarikci = $"{sourceOrder.OrderCode} Siparişinden Transfer"
                };
                _context.StokHareketler.Add(giris);

                _context.StokKartlari.Update(stok);
                _context.SaveChanges();
                transaction.Commit();

                return Json(new { success = true, message = "Stok transferi başarıyla gerçekleşti." });
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
