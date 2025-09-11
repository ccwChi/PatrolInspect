using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;
using PatrolInspect.Repository;
using System.Globalization;
using System.Text;

namespace PatrolInspect.Controllers
{
    public class ScheduleController : Controller
    {
        private readonly IScheduleRepository _scheduleRepository;
        private readonly ILogger<ScheduleController> _logger;

        public ScheduleController(IScheduleRepository scheduleRepository, ILogger<ScheduleController> logger)
        {
            _scheduleRepository = scheduleRepository;
            _logger = logger;
        }

        // 顯示編輯頁面
        [HttpGet]
        public IActionResult Edit()
        {
            // 檢查登入狀態
            var userNo = HttpContext.Session.GetString("UserNo");
            if (string.IsNullOrEmpty(userNo))
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.CurrentUser = userNo;
            ViewBag.CurrentUserName = HttpContext.Session.GetString("UserName");
            return View();
        }


        [HttpGet]
        public async Task<IActionResult> GetBaseInfo()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            if (string.IsNullOrEmpty(userNo))
            {
                return Json(new { success = false, message = "未登入" });
            }

            try
            {
                var scheduleBase = await _scheduleRepository.GetScheduleBaseInfoAsync();
                var areas = scheduleBase.Areas;
                var allUsers = scheduleBase.Users;
                var eventTypes = scheduleBase.EventTypes;
                var scheduleUsers = scheduleBase.ScheduleUserNames;
                var scheduleDeparts = scheduleBase.ScheduleDepartments;

                return Json(new
                {
                    success = true,
                    data = new
                    {
                        areas,
                        allUsers,
                        scheduleUsers,
                        scheduleDeparts,
                        eventTypes
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting Areas Users InspectType");
                return Json(new { success = false, message = "載入基礎資料失敗" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveSchedules([FromBody] List<InspectionScheduleEvent> schedules)
        {
            try
            {
                var currentUser = HttpContext.Session.GetString("UserNo");

                // 轉換DTO到實體
                var scheduleEntities = schedules.Select(dto => new InspectionScheduleEvent
                {
                    UserNo = dto.UserNo,
                    UserName = dto.UserName,
                    Department = dto.Department,
                    EventType = dto.EventType,
                    EventDetail = dto.EventDetail,
                    StartDateTime = dto.StartDateTime,
                    EndDateTime = dto.EndDateTime,
                    Area = dto.Area,
                    CreateBy = currentUser
                }).ToList();

                var eventIds = await _scheduleRepository.CreateSchedulesBatchAsync(scheduleEntities);

                _logger.LogInformation("Successfully saved {Count} schedules by user: {User}", eventIds.Count, currentUser);

                return Json(new
                {
                    success = true,
                    message = $"成功儲存 {eventIds.Count} 筆排班資料",
                    eventIds = eventIds
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving schedules");
                return Json(new { success = false, message = "儲存排班資料失敗: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteSchedules([FromBody] List<int> eventIds)
        {
            try
            {
                var success = await _scheduleRepository.DeleteSchedulesBatchAsync(eventIds);

                if (success)
                {
                    _logger.LogInformation("Successfully deleted {Count} schedules", eventIds.Count);
                    return Json(new { success = true, message = $"成功刪除 {eventIds.Count} 筆排班資料" });
                }
                else
                {
                    return Json(new { success = false, message = "刪除失敗" });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedules");
                return Json(new { success = false, message = "刪除排班資料失敗: " + ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> GetSearchSchedules(string userName, string depart, DateTime startDate, DateTime endDate)
        {
            try
            {
                var schedules = await _scheduleRepository.GetSearchSchedules(userName, depart, startDate, endDate);
                return Json(new { success = true, data = schedules });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user schedules for: {userName}", userName);
                return Json(new { success = false, message = "搜尋資料失敗" });
            }
        }

        //////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////////////////////////
        [HttpGet]
        public async Task<IActionResult> ExportSchedulesToExcel(string userName, string depart, DateTime startDate, DateTime endDate)
        {
            try
            {
                var userNo = HttpContext.Session.GetString("UserNo");
                if (string.IsNullOrEmpty(userNo))
                {
                    return Json(new { success = false, message = "未登入" });
                }

                // 使用相同的搜尋條件取得資料
                var schedules = await _scheduleRepository.GetSearchSchedules(userName, depart, startDate, endDate);

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("排班資料");

                // 設定表頭
                //worksheet.Cell(1, 1).Value = "EventId";
                worksheet.Cell(1, 1).Value = "人員工號";
                worksheet.Cell(1, 2).Value = "人員姓名";
                worksheet.Cell(1, 3).Value = "部門";
                worksheet.Cell(1, 4).Value = "區域";
                worksheet.Cell(1, 5).Value = "檢驗項目";
                worksheet.Cell(1, 6).Value = "檢驗項目描述";
                worksheet.Cell(1, 7).Value = "開始時間";
                worksheet.Cell(1, 8).Value = "結束時間";

                // 設定表頭樣式
                //var headerRange = worksheet.Range(1, 1, 1, 10);
                //headerRange.Style.Font.Bold = true;
                //headerRange.Style.Fill.BackgroundColor = XLColor.LightBlue;
                //headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thick;

                // 填入資料
                for (int i = 0; i < schedules.Count; i++)
                {
                    var row = i + 2;
                    var schedule = schedules[i];

                    //worksheet.Cell(row, 1).Value = schedule.EventId;
                    worksheet.Cell(row, 1).Value = schedule.UserNo;
                    worksheet.Cell(row, 2).Value = schedule.UserName;
                    worksheet.Cell(row, 3).Value = schedule.Department;
                    worksheet.Cell(row, 4).Value = schedule.Area;
                    worksheet.Cell(row, 5).Value = schedule.EventType ?? "";
                    worksheet.Cell(row, 6).Value = schedule.EventDetail ?? "";
                    worksheet.Cell(row, 7).Value = schedule.StartDateTime;
                    worksheet.Cell(row, 8).Value = schedule.EndDateTime;
                }

                // 自動調整欄寬
                worksheet.Columns().AdjustToContents();

                // 設定資料範圍邊框
                //if (schedules.Count > 0)
                //{
                //    var dataRange = worksheet.Range(1, 1, schedules.Count + 1, 10);
                //    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                //    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                //}

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                stream.Position = 0;

                var fileName = $"排班資料_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
                _logger.LogInformation("Export Excel file: {FileName} by user: {UserNo}", fileName, userNo);

                return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting schedules to Excel");
                return Json(new { success = false, message = "匯出失敗: " + ex.Message });
            }
        }

        // 匯入 Excel
        [HttpPost]
        public async Task<IActionResult> ImportSchedulesFromExcel(IFormFile file)
        {
            try
            {
                var userNo = HttpContext.Session.GetString("UserNo");
                if (string.IsNullOrEmpty(userNo))
                {
                    return Json(new { success = false, message = "未登入" });
                }

                if (file == null || file.Length == 0)
                {
                    return Json(new { success = false, message = "請選擇要匯入的 Excel 檔案" });
                }

                if (!file.FileName.EndsWith(".xlsx") && !file.FileName.EndsWith(".xls"))
                {
                    return Json(new { success = false, message = "請上傳 Excel 格式檔案 (.xlsx 或 .xls)" });
                }

                var schedules = new List<InspectionScheduleEvent>();

                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheet(1);

                var rowCount = worksheet.LastRowUsed().RowNumber();

                if (rowCount < 2)
                {
                    return Json(new { success = false, message = "Excel 檔案沒有資料" });
                }

                // 從第二列開始讀取資料 (第一列是表頭)
                for (int row = 2; row <= rowCount; row++)
                {
                    try
                    {
                        var schedule = new InspectionScheduleEvent
                        {
                            UserNo = worksheet.Cell(row, 1).GetString(),
                            UserName = worksheet.Cell(row, 2).GetString(),
                            Department = worksheet.Cell(row, 3).GetString(),
                            Area = worksheet.Cell(row, 4).GetString(),
                            EventType = worksheet.Cell(row, 5).GetString(),
                            EventDetail = worksheet.Cell(row, 6).GetString(),
                            StartDateTime = worksheet.Cell(row, 7).GetDateTime(),
                            EndDateTime = worksheet.Cell(row, 8).GetDateTime(),
                            CreateBy = userNo
                        };

                        // 基本驗證
                        if (string.IsNullOrWhiteSpace(schedule.UserNo) ||
                            string.IsNullOrWhiteSpace(schedule.UserName) ||
                            string.IsNullOrWhiteSpace(schedule.Area))
                        {
                            return Json(new { success = false, message = $"第 {row} 列資料不完整" });
                        }

                        if (schedule.StartDateTime >= schedule.EndDateTime)
                        {
                            return Json(new { success = false, message = $"第 {row} 列時間設定錯誤：結束時間必須大於開始時間" });
                        }

                        schedules.Add(schedule);
                    }
                    catch (Exception ex)
                    {
                        return Json(new { success = false, message = $"第 {row} 列資料格式錯誤: {ex.Message}" });
                    }
                }

                if (schedules.Count == 0)
                {
                    return Json(new { success = false, message = "沒有有效的資料可以匯入" });
                }

                // 批次新增到資料庫
                var eventIds = await _scheduleRepository.CreateSchedulesBatchAsync(schedules);

                _logger.LogInformation("Successfully imported {Count} schedules from Excel by user: {UserNo}", eventIds.Count, userNo);

                return Json(new
                {
                    success = true,
                    message = $"成功匯入 {eventIds.Count} 筆排班資料",
                    importedCount = eventIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing schedules from Excel");
                return Json(new { success = false, message = "匯入失敗: " + ex.Message });
            }
        }

        //[HttpGet]
        //public async Task<IActionResult> ExportCsv(string userName, string depart, DateTime startDate, DateTime endDate)
        //{
        //    try
        //    {
        //        var schedules = await _scheduleRepository.GetSearchSchedules(userName, depart, startDate, endDate);

        //        if (!schedules.Any())
        //        {
        //            return Json(new { success = false, message = "沒有資料可以匯出" });
        //        }

        //        var csv = GenerateCsvContent(schedules);
        //        var fileName = $"排班資料_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
        //        var big5Encoding = Encoding.GetEncoding("big5");
        //        return File(big5Encoding.GetBytes(csv), "text/csv; charset=big5", fileName);
        //        //return File(Encoding.UTF8.GetBytes(csv), "text/csv; charset=utf-8", fileName);
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error exporting CSV");
        //        return Json(new { success = false, message = "匯出失敗: " + ex.Message });
        //    }
        //}

        //[HttpPost]
        //public async Task<IActionResult> ImportCsv(IFormFile csvFile)
        //{
        //    try
        //    {
        //        if (csvFile == null || csvFile.Length == 0)
        //        {
        //            return Json(new { success = false, message = "請選擇 CSV 檔案" });
        //        }

        //        if (!csvFile.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        //        {
        //            return Json(new { success = false, message = "請上傳 CSV 格式檔案" });
        //        }

        //        var schedules = new List<InspectionScheduleEvent>();
        //        var invalidRows = new List<string>();
        //        var currentUser = HttpContext.Session.GetString("UserNo");

        //        using (var reader = new StreamReader(csvFile.OpenReadStream(), Encoding.GetEncoding("big5")))
        //        {
        //            var header = await reader.ReadLineAsync();
        //            //if (header != GetCsvHeader())
        //            //{
        //            //    return Json(new { success = false, message = "CSV 檔案格式不正確，請使用系統匯出的範本" });
        //            //}

        //            int rowNumber = 2; // 從第二行開始（第一行是標題）
        //            string line;
        //            while ((line = await reader.ReadLineAsync()) != null)
        //            {
        //                try
        //                {
        //                    var schedule = ParseCsvLine(line, currentUser);
        //                    if (schedule != null)
        //                    {
        //                        schedules.Add(schedule);
        //                    }
        //                }
        //                catch (Exception ex)
        //                {
        //                    invalidRows.Add($"第 {rowNumber} 行: {ex.Message}");
        //                }
        //                rowNumber++;
        //            }
        //        }

        //        if (invalidRows.Any())
        //        {
        //            return Json(new
        //            {
        //                success = false,
        //                message = "CSV 檔案包含錯誤資料",
        //                details = invalidRows
        //            });
        //        }

        //        if (!schedules.Any())
        //        {
        //            return Json(new { success = false, message = "CSV 檔案中沒有有效的排班資料" });
        //        }

        //        var eventIds = await _scheduleRepository.CreateSchedulesBatchAsync(schedules);

        //        _logger.LogInformation("Successfully imported {Count} schedules from CSV by user: {User}",
        //            eventIds.Count, currentUser);

        //        return Json(new
        //        {
        //            success = true,
        //            message = $"成功匯入 {eventIds.Count} 筆排班資料",
        //            importedCount = eventIds.Count
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        _logger.LogError(ex, "Error importing CSV");
        //        return Json(new { success = false, message = "匯入失敗: " + ex.Message });
        //    }
        //}

        //private string GenerateCsvContent(List<InspectionScheduleEvent> schedules)
        //{
        //    var sb = new StringBuilder();

        //    // CSV 標題行
        //    sb.AppendLine(GetCsvHeader());

        //    // 資料行
        //    foreach (var schedule in schedules)
        //    {
        //        sb.AppendLine($"\"{EscapeCsvField(schedule.UserNo)}\"," +
        //                     $"\"{EscapeCsvField(schedule.UserName)}\"," +
        //                     $"\"{EscapeCsvField(schedule.Department)}\"," +
        //                     $"\"{EscapeCsvField(schedule.EventType)}\"," +
        //                     $"\"{EscapeCsvField(schedule.EventTypeName)}\"," +
        //                     $"\"{EscapeCsvField(schedule.EventDetail ?? "")}\"," +
        //                     $"\"{schedule.StartDateTime:yyyy-MM-dd}\"," +
        //                     $"\"{schedule.StartDateTime:HH:mm}\"," +
        //                     $"\"{schedule.EndDateTime:yyyy-MM-dd}\"," +
        //                     $"\"{schedule.EndDateTime:HH:mm}\"," +
        //                     $"\"{EscapeCsvField(schedule.Area)}\"");
        //    }

        //    return sb.ToString();
        //}

        //private string GetCsvHeader()
        //{
        //    return "工號,姓名,部門,事件類型,事件類型名稱,詳細內容,開始日期,開始時間,結束日期,結束時間,區域";
        //}

        //private string EscapeCsvField(string field)
        //{
        //    if (string.IsNullOrEmpty(field))
        //        return "";

        //    // 處理 CSV 中的特殊字符
        //    if (field.Contains("\""))
        //        field = field.Replace("\"", "\"\"");

        //    return field;
        //}

        //private InspectionScheduleEvent? ParseCsvLine(string line, string currentUser)
        //{
        //    var fields = ParseCsvFields(line);

        //    if (fields.Length != 11)
        //    {
        //        throw new ArgumentException("欄位數量不正確，應為 11 個欄位");
        //    }

        //    // 驗證必填欄位
        //    if (string.IsNullOrWhiteSpace(fields[0])) // 工號
        //        throw new ArgumentException("工號不能為空");
        //    if (string.IsNullOrWhiteSpace(fields[1])) // 姓名
        //        throw new ArgumentException("姓名不能為空");
        //    if (string.IsNullOrWhiteSpace(fields[6]) && string.IsNullOrWhiteSpace(fields[7])) // 開始日期
        //        throw new ArgumentException("開始日期不能為空");
        //    if (string.IsNullOrWhiteSpace(fields[8]) && string.IsNullOrWhiteSpace(fields[9])) // 結束日期
        //        throw new ArgumentException("結束日期不能為空");

        //    try
        //    {
        //        var schedule = new InspectionScheduleEvent
        //        {
        //            UserNo = fields[0].Trim(),
        //            UserName = fields[1].Trim(),
        //            Department = fields[2].Trim(),
        //            EventType = fields[3].Trim(),
        //            EventTypeName = fields[4].Trim(),
        //            EventDetail = string.IsNullOrWhiteSpace(fields[5]) ? null : fields[5].Trim(),
        //            Area = fields[10].Trim(),
        //            IsActive = true,
        //            CreateBy = currentUser
        //        };

        //        // 解析日期時間
        //        if (!DateTime.TryParse(fields[6], out var startDate))
        //            throw new ArgumentException($"開始日期格式錯誤: {fields[6]}");
        //        if (!DateTime.TryParse(fields[8], out var endDate))
        //            throw new ArgumentException($"結束日期格式錯誤: {fields[8]}");

        //        var startTime = string.IsNullOrWhiteSpace(fields[7]) ? "00:00" : fields[7];
        //        var endTime = string.IsNullOrWhiteSpace(fields[9]) ? "23:59" : fields[9];

        //        if (!TimeSpan.TryParse(startTime, out var startTimeSpan))
        //            throw new ArgumentException($"開始時間格式錯誤: {fields[7]}");
        //        if (!TimeSpan.TryParse(endTime, out var endTimeSpan))
        //            throw new ArgumentException($"結束時間格式錯誤: {fields[9]}");

        //        schedule.StartDateTime = startDate.Date.Add(startTimeSpan);
        //        schedule.EndDateTime = endDate.Date.Add(endTimeSpan);

        //        // 驗證時間邏輯
        //        if (schedule.EndDateTime <= schedule.StartDateTime)
        //            throw new ArgumentException("結束時間必須晚於開始時間");

        //        return schedule;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new ArgumentException($"資料解析錯誤: {ex.Message}");
        //    }
        //}

        //private string[] ParseCsvFields(string line)
        //{
        //    var fields = new List<string>();
        //    var currentField = new StringBuilder();
        //    bool inQuotes = false;

        //    for (int i = 0; i < line.Length; i++)
        //    {
        //        char c = line[i];

        //        if (c == '"')
        //        {
        //            if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
        //            {
        //                // 雙引號轉義
        //                currentField.Append('"');
        //                i++; // 跳過下一個引號
        //            }
        //            else
        //            {
        //                inQuotes = !inQuotes;
        //            }
        //        }
        //        else if (c == ',' && !inQuotes)
        //        {
        //            fields.Add(currentField.ToString());
        //            currentField.Clear();
        //        }
        //        else
        //        {
        //            currentField.Append(c);
        //        }
        //    }

        //    // 加入最後一個欄位
        //    fields.Add(currentField.ToString());

        //    return fields.ToArray();
        //}
    }
}