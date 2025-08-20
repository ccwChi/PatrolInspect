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
});

// Register Repository (DI)
builder.Services.AddScoped<IUserRepository, UserRepository>();

// Add Logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

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

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

logger.LogInformation("=== InspectionSystem Started Successfully ===");

app.Run();