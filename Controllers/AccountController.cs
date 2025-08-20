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
            // 檢查是否已登入
            var userNo = HttpContext.Session.GetString("UserNo");
            if (!string.IsNullOrEmpty(userNo))
            {
                _logger.LogInformation("User already logged in, redirecting to dashboard: {UserNo}", userNo);
                return RedirectToAction("Dashboard", "Inspection");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "請填入工號" });
            }

            try
            {
                var userNo = model.UserNo.Trim().ToUpper();

                if (string.IsNullOrWhiteSpace(userNo))
                {
                    return Json(new { success = false, message = "請輸入工號" });
                }

                // 驗證使用者登入
                var (success, message, user) = await _userRepository.ValidateUserLoginAsync(userNo);

                if (!success || user == null)
                {
                    _logger.LogWarning("Login failed for UserNo: {UserNo}, Reason: {Message}", userNo, message);
                    return Json(new { success = false, message = message });
                }

                // 設定 Session
                SetUserSession(user);

                _logger.LogInformation("User logged in successfully: {UserNo} - {UserName}", user.UserNo, user.UserName);

                return Json(new
                {
                    success = true,
                    message = "登入成功",
                    redirectUrl = Url.Action("Dashboard", "Inspection")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error for UserNo: {UserNo}", model.UserNo);
                return Json(new { success = false, message = "系統發生錯誤，請稍後再試" });
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
            return RedirectToAction("Login");
        }

        private void SetUserSession(MesUser user)
        {
            HttpContext.Session.SetString("UserNo", user.UserNo);
            HttpContext.Session.SetString("UserName", user.UserName);
            HttpContext.Session.SetString("DepartmentName", user.DepartmentName);
            HttpContext.Session.SetString("FatherDepartmentName", user.FatherDepartmentName ?? "");
            HttpContext.Session.SetString("TitleName", user.TitleName);
            HttpContext.Session.SetString("LoginTime", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
        }

        // 檢查登入狀態的輔助方法
        [HttpGet]
        public IActionResult CheckLoginStatus()
        {
            var userInfo = new
            {
                IsLoggedIn = !string.IsNullOrEmpty(HttpContext.Session.GetString("UserNo")),
                UserNo = HttpContext.Session.GetString("UserNo"),
                UserName = HttpContext.Session.GetString("UserName"),
                DepartmentName = HttpContext.Session.GetString("DepartmentName"),
                LoginTime = HttpContext.Session.GetString("LoginTime")
            };

            return Json(userInfo);
        }
    }
}