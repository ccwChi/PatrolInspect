using Microsoft.AspNetCore.Mvc;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using PatrolInspect.Repositories.Interfaces;

namespace PatrolInspect.Controllers
{
    public class ItemManageController : Controller
    {
        private readonly IItemManageRepository _itemManageRepository;
        private readonly ILogger<ItemManageController> _logger;

        public ItemManageController(IItemManageRepository itemManageRepository, ILogger<ItemManageController> logger)
        {
            _itemManageRepository = itemManageRepository;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // 檢查登入狀態
            var userNo = HttpContext.Session.GetString("UserNo");
            if (string.IsNullOrEmpty(userNo))
                return RedirectToAction("Login", "Account");

            // 設定使用者基本資訊到 ViewBag
            ViewBag.UserNo = userNo;
            ViewBag.UserName = HttpContext.Session.GetString("UserName");
            ViewBag.DepartmentName = HttpContext.Session.GetString("DepartmentName");
            ViewBag.TitleName = HttpContext.Session.GetString("TitleName");
            ViewBag.CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetInspectionItems([FromBody] InspectionItemQueryDto query)
        {
            try
            {
                var userNo = HttpContext.Session.GetString("UserNo");
                if (string.IsNullOrEmpty(userNo))
                {
                    return Json(new { success = false, message = "未登入" });
                }

                var result = await _itemManageRepository.GetInspectionItemsAsync(query);
                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection items: {@Query}", query);
                return Json(new { success = false, message = "撈取資料失敗: " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetInspectionItem(int id)
        {
            try
            {
                var userNo = HttpContext.Session.GetString("UserNo");
                if (string.IsNullOrEmpty(userNo))
                    return Json(new { success = false, message = "未登入" });

                var item = await _itemManageRepository.GetInspectionItemByIdAsync(id);
                if (item == null)
                    return Json(new { success = false, message = "找不到指定的檢驗項目" });

                return Json(new { success = true, data = item });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspection item by ID: {ItemId}", id);
                return Json(new { success = false, message = "取得資料失敗：" + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateInspectionItem([FromBody] InspectionItemCreateDto dto)
        {
            try
            {
                var userNo = HttpContext.Session.GetString("UserNo");
                var userName = HttpContext.Session.GetString("UserName");
                if (string.IsNullOrEmpty(userNo))
                    return Json(new { success = false, message = "未登入" });

                // 驗證模型
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                    return Json(new { success = false, message = "輸入資料有誤：" + string.Join(", ", errors) });
                }

                // 驗證選項內容
                if (!dto.IsValid())
                {
                    return Json(new { success = false, message = "下拉選單類型必須填入選項內容" });
                }

                // 檢查名稱是否重複
                var isExists = await _itemManageRepository.IsInspectionItemNameExistsAsync(
                    dto.InspectName.Trim(), dto.Department, dto.InspectArea);
                if (isExists)
                {
                    return Json(new { success = false, message = "該部門區域已存在相同名稱的檢驗項目" });
                }

                // 建立實體
                var item = new InspectionItem
                {
                    InspectName = dto.InspectName.Trim(),
                    Department = dto.Department,
                    InspectArea = dto.InspectArea,
                    Station = string.IsNullOrWhiteSpace(dto.Station) ? null : dto.Station.Trim(),
                    DataType = dto.DataType,
                    SelectOptions = string.IsNullOrWhiteSpace(dto.SelectOptions) ? null : dto.SelectOptions.Trim(),
                    IsRequired = dto.IsRequired,
                    CreateBy = $"{userNo}-{userName}",
                    IsActive = true
                };

                var itemId = await _itemManageRepository.CreateInspectionItemAsync(item);

                _logger.LogInformation("Created inspection item: {ItemId} - {InspectName} by {UserNo}",
                    itemId, item.InspectName, userNo);

                return Json(new { success = true, message = "新增成功", data = new { itemId = itemId } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating inspection item: {@Dto}", dto);
                return Json(new { success = false, message = "新增失敗：" + ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> UpdateInspectionItem([FromBody] InspectionItemUpdateDto dto)
        {
            try
            {
                var userNo = HttpContext.Session.GetString("UserNo");
                var userName = HttpContext.Session.GetString("UserName");
                if (string.IsNullOrEmpty(userNo))
                    return Json(new { success = false, message = "未登入" });

                // 驗證模型
                if (!ModelState.IsValid)
                {
                    var errors = ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage));
                    return Json(new { success = false, message = "輸入資料有誤：" + string.Join(", ", errors) });
                }

                // 驗證選項內容
                if (!dto.IsValid())
                {
                    return Json(new { success = false, message = "下拉選單類型必須填入選項內容" });
                }

                // 檢查項目是否存在
                var existingItem = await _itemManageRepository.GetInspectionItemByIdAsync(dto.ItemId);
                if (existingItem == null)
                {
                    return Json(new { success = false, message = "找不到指定的檢驗項目" });
                }

                // 檢查名稱是否重複（排除自己）
                var isExists = await _itemManageRepository.IsInspectionItemNameExistsAsync(
                    dto.InspectName.Trim(), dto.Department, dto.InspectArea, dto.ItemId);
                if (isExists)
                {
                    return Json(new { success = false, message = "該部門區域已存在相同名稱的檢驗項目" });
                }

                // 更新實體
                existingItem.InspectName = dto.InspectName.Trim();
                existingItem.Department = dto.Department;
                existingItem.InspectArea = dto.InspectArea;
                existingItem.Station = string.IsNullOrWhiteSpace(dto.Station) ? null : dto.Station.Trim();
                existingItem.DataType = dto.DataType;
                existingItem.SelectOptions = string.IsNullOrWhiteSpace(dto.SelectOptions) ? null : dto.SelectOptions.Trim();
                existingItem.IsRequired = dto.IsRequired;
                existingItem.UpdateBy = $"{userNo}-{userName}";
                existingItem.UpdateReason = dto.UpdateReason?.Trim();

                var success = await _itemManageRepository.UpdateInspectionItemAsync(existingItem);
                if (!success)
                {
                    return Json(new { success = false, message = "更新失敗" });
                }

                _logger.LogInformation("Updated inspection item: {ItemId} - {InspectName} by {UserNo}",
                    dto.ItemId, existingItem.InspectName, userNo);

                return Json(new { success = true, message = "更新成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating inspection item: {@Dto}", dto);
                return Json(new { success = false, message = "更新失敗：" + ex.Message });
            }
        }


        [HttpPost]
        public async Task<IActionResult> ToggleInspectionItemStatus([FromBody] InspectionItemStatusDto dto)
        {
            try
            {
                var userNo = HttpContext.Session.GetString("UserNo");
                var userName = HttpContext.Session.GetString("UserName");
                if (string.IsNullOrEmpty(userNo))
                    return Json(new { success = false, message = "未登入" });

                // 檢查項目是否存在
                var existingItem = await _itemManageRepository.GetInspectionItemByIdAsync(dto.ItemId);
                if (existingItem == null)
                {
                    return Json(new { success = false, message = "找不到指定的檢驗項目" });
                }

                var success = await _itemManageRepository.ToggleInspectionItemStatusAsync(
                    dto.ItemId, dto.IsActive, $"{userNo}-{userName}", dto.UpdateReason?.Trim());

                if (!success)
                {
                    return Json(new { success = false, message = "狀態變更失敗" });
                }

                var action = dto.IsActive ? "啟用" : "停用";
                _logger.LogInformation("{Action} inspection item: {ItemId} by {UserNo}",
                    action, dto.ItemId, userNo);

                return Json(new { success = true, message = $"{action}成功" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling inspection item status: {@Dto}", dto);
                return Json(new { success = false, message = "狀態變更失敗：" + ex.Message });
            }
        }


        [HttpDelete]
        public async Task<IActionResult> DeleteInspectionItem(int id)
        {
            try
            {
                // 檢查登入狀態
                var userNo = HttpContext.Session.GetString("UserNo");
                if (string.IsNullOrEmpty(userNo))
                    return Json(ApiResponse<object>.ErrorResult("登入已過期，請重新登入", "UNAUTHORIZED"));

                // 檢查項目是否存在
                var existingItem = await _itemManageRepository.GetInspectionItemByIdAsync(id);
                if (existingItem == null)
                {
                    return Json(ApiResponse<object>.ErrorResult("找不到指定的檢驗項目", "NOT_FOUND"));
                }

                var success = await _itemManageRepository.DeleteInspectionItemAsync(id);
                if (!success)
                {
                    return Json(ApiResponse<object>.ErrorResult("刪除失敗", "DELETE_FAILED"));
                }

                _logger.LogInformation("Deleted inspection item: {ItemId} by {UserNo}", id, userNo);

                return Json(ApiResponse<object>.SuccessResult(null, "刪除成功"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting inspection item: {ItemId}", id);
                return Json(ApiResponse<object>.ErrorResult("刪除失敗：" + ex.Message, "SERVER_ERROR"));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartments()
        {
            try
            {
                // 固定的部門選項，也可以從資料庫動態取得
                var departments = new[] { "QA", "QC", "環安", "生產" };
                return Json(ApiResponse<string[]>.SuccessResult(departments));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting departments");
                return Json(ApiResponse<object>.ErrorResult("取得部門清單失敗：" + ex.Message, "SERVER_ERROR"));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetInspectAreas()
        {
            try
            {
                // 固定的區域選項，也可以從資料庫動態取得
                var areas = new[] { "all", "射出一區", "射出二區", "射出三區", "復興廠射出", "無塵室" };
                return Json(ApiResponse<string[]>.SuccessResult(areas));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting inspect areas");
                return Json(ApiResponse<object>.ErrorResult("取得區域清單失敗：" + ex.Message, "SERVER_ERROR"));
            }
        }

        [HttpPost]
        public IActionResult Logout()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            if (!string.IsNullOrEmpty(userNo))
            {
                _logger.LogInformation("User logged out: {UserNo}", userNo);
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Account");
        }
    }
}