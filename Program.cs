using PatrolInspect.Models;
using PatrolInspect.Repositories.Interfaces;
using PatrolInspect.Repository;

var builder = WebApplication.CreateBuilder(args);

// �]�w IIS �����Ҧ��A���Τ��{�������Ҽv�T
builder.WebHost.UseIIS();
builder.Services.Configure<IISServerOptions>(options =>
{
    options.AutomaticAuthentication = false;  // ����G���� IIS ����
    options.AllowSynchronousIO = true;        // ���\�P�B IO
});

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
builder.Services.AddScoped<IScheduleRepository, ScheduleRepository>();
builder.Services.AddScoped<IInspectionRepository, InspectionRepository>();
builder.Services.AddScoped<IInspectionItemRepository, InspectionItemRepository>();

// Add Logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

// Add CORS (�p�G�ݭn)
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
    // ���� HSTS�A�]�����{���w�g�B�z
    // app.UseHsts();
    // �ȮɫO�d�Բӿ��~�H�K�����A������i����
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseDeveloperExceptionPage();
}

// ���n�G���� HTTPS ���w�V�A�]��IIS���{���w�g�B�z HTTPS
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

//app.UseCors("AllowAll");

app.UseSession();

// ���� UseAuthorization�A���ϥ�����
// app.UseAuthorization();

// �s�W API ���� (�p�G�ݭn)
//app.MapControllerRoute(
//    name: "api",
//    pattern: "api/{controller}/{action=Index}/{id?}");

// �w�]����
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}");

// �ڸ��|���ɦV
//app.MapGet("/", () => Results.Redirect("/Account/Login"));

logger.LogInformation("=== InspectionSystem Started Successfully ===");

app.Run();