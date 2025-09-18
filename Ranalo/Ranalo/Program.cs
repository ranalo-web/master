using Ranalo.DataStore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.SqlServer;
using Ranalo.Services;
using System.Data;
using System.Data.SqlClient;
using Ranalo;
using Ranalo.ScheduledServices;
using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddControllersWithViews();

builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register your repository
builder.Services.AddScoped<IWooOrderRepository, WooOrderRepository>();
builder.Services.AddScoped<ISyncLogsRepository, SyncLogsRepository>();
builder.Services.AddScoped<IWooOrderProductRepository, WooOrderProductRepository>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IKosePaymentsRepository, KosePaymentsRepository>();


builder.Services.AddScoped<IRepository, Repository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IApplicationReportService, ApplicationReportService>();
builder.Services.AddScoped<IApplicationReportRepository, ApplicationReportRepository>();
builder.Services.AddScoped<IContractCalculatorService, ContractCalculatorService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IDevicesRepository, DevicesRepository>();
builder.Services.AddScoped<IStatementService, StatementService>();
builder.Services.AddScoped<IStatementsRepository, StatementsRepository>();
//Task<IEnumerable<MobileStatusReport>> GetStatusReportByDealer(int deviceGroupId)
//IUserService
builder.Services.AddHostedService<ScheduledTaskDeviceUnlockService>();
builder.Services.AddHostedService<ScheduledTaskPaymentsService>();
builder.Services.AddHostedService<ScheduledTaskWooOrdersService>();


builder.Services.AddDistributedMemoryCache(); // or use Redis, etc.
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();
app.UseMiddleware<UserSettingsMiddleware>();
app.MapRazorPages();
//app.MapFallbackToPage("/Pages/Login");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();
