using PatrolInspect.Models;
using PatrolInspect.Repositories.Interfaces;
using PatrolInspect.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

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
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
});

// Register Repository (DI)
builder.Services.AddScoped<IUserRepository, UserRepository>();

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

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

//app.UseCors("AllowAll");

app.UseSession();
app.UseAuthorization();

// 新增 API 路由 (如果需要)
//app.MapControllerRoute(
//    name: "api",
//    pattern: "api/{controller}/{action=Index}/{id?}");

// 預設路由
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// 根路徑重導向
app.MapGet("/", () => Results.Redirect("/Account/Login"));

logger.LogInformation("=== InspectionSystem Started Successfully ===");

app.Run();