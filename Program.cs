using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PatrolInspect.Models;
using PatrolInspect.Repositories.Interfaces;
using PatrolInspect.Repository;

var builder = WebApplication.CreateBuilder(args);

// 設定 IIS 集成模式，停用父程式的驗證影響
builder.WebHost.UseIIS();
builder.Services.Configure<IISServerOptions>(options =>
{
    options.AutomaticAuthentication = false;  // 關鍵：停用 IIS 驗證
    options.AllowSynchronousIO = true;        // 允許同步 IO
});

// Add services to the container
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new IgnoreAntiforgeryTokenAttribute());
});

// Configure AppSettings
builder.Services.Configure<AppSettings>(
    builder.Configuration.GetSection("AppSettings"));

// Add Session support
builder.Services.AddSession(options =>
{
    var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>();
    options.IdleTimeout = TimeSpan.FromMinutes(appSettings?.SessionTimeout ?? 480); // 8 hours default
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Register Repository (DI)
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IInspectionRepository, InspectionRepository>();
builder.Services.AddScoped<IItemManageRepository, ItemManageRepository>();

// Add Logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// Add CORS (如果需要)
//builder.Services.AddCors(options =>
//{
//    options.AddPolicy("AllowAll",
//        builder =>
//        {
//            builder
//                .AllowAnyOrigin()
//                .AllowAnyMethod()
//                .AllowAnyHeader();
//        });
//});


var app = builder.Build();

// Log startup environment info
var logger = app.Services.GetRequiredService<ILogger<Program>>();
var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>();
if (appSettings != null)
{
    var envName = EnvironmentHelper.GetEnvironmentName(appSettings.EnvFlag);
    logger.LogInformation("=== InspectionSystem Starting ===");
    logger.LogInformation("Environment: {EnvName} (EnvFlag: {EnvFlag})", envName, appSettings.EnvFlag);
    logger.LogInformation("Session Timeout: {SessionTimeout} minutes", appSettings.SessionTimeout);
}


app.UsePathBase("/GudengMesPortal/PatrolInspect");

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // 移除 HSTS，因為父程式已經處理
    // app.UseHsts();
    // 暫時保留詳細錯誤以便除錯，完成後可移除
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseDeveloperExceptionPage();
}

// 重要：移除 HTTPS 重定向，因為IIS父程式已經處理 HTTPS
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

//app.UseCors("AllowAll");

app.UseSession();

// 移除 UseAuthorization，不使用驗證
// app.UseAuthorization();

// 新增 API 路由 (如果需要)
//app.MapControllerRoute(
//    name: "api",
//    pattern: "api/{controller}/{action=Index}/{id?}");

// 預設路由
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// 根路徑重導向
//app.MapGet("/", () => Results.Redirect("/Account/Login"));

logger.LogInformation("=== InspectionSystem Started Successfully ===");

app.Run();