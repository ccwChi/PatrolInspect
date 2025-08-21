using PatrolInspect.Models;
using Microsoft.AspNetCore.Mvc;
using PatrolInspect.Repositories.Interfaces;

namespace PatrolInspect.Controllers
{
    public class AccountController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<AccountController> _logger;

        public AccountController(IUserRepository userRepository, ILogger<AccountController> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Login()
        {
            // 如果已登入就直接跳轉
            var userNo = HttpContext.Session.GetString("UserNo");
            if (!string.IsNullOrEmpty(userNo))
            {
                //return RedirectToAction("Index", "Inspection");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginViewModel loginData)
        {
            try
            {
                // 直接從動態物件取得工號
                string userNo = loginData?.UserNo?.ToString()?.Trim()?.ToUpper() ?? "";

                _logger.LogInformation("Login attempt: {UserNo}", userNo);

                // 簡單驗證
                if (string.IsNullOrEmpty(userNo))
                {
                    return Json(new { success = false, message = "請輸入工號" });
                }

                // 驗證使用者
                var (success, message, user) = await _userRepository.ValidateUserLoginAsync(userNo);

                if (!success || user == null)
                {
                    _logger.LogWarning("Login failed: {UserNo} - {Message}", userNo, message);
                    return Json(new { success = false, message = message });
                }

                // 設定 Session
                SetUserSession(user);

                _logger.LogInformation("Login success: {UserNo} - {UserName}", user.UserNo, user.UserName);

                return Json(new
                {
                    success = true,
                    message = "登入成功",
                    redirectUrl = Url.Action("Index", "Inspection")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                return Json(new { success = false, message = "系統錯誤，請稍後再試" });
            }
        }

        // 簡化版登出
        [HttpPost]
        public IActionResult Logout()
        {
            var userNo = HttpContext.Session.GetString("UserNo");
            if (!string.IsNullOrEmpty(userNo))
            {
                _logger.LogInformation("User logout: {UserNo}", userNo);
            }

            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }

        // 私有方法保持不變
        private void SetUserSession(MesUser user)
        {
            HttpContext.Session.SetString("UserNo", user.UserNo);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("DepartmentName", user.DepartmentName);
            HttpContext.Session.SetString("TitleName", user.TitleName);
            HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }
    }
}