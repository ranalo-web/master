using Ranalo.Woocommece.Api.DataStore;
using Ranalo.Woocommece.Api.Services;
using System.Data;
using System.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IDbConnection>(sp =>
    new SqlConnection(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register your repository
builder.Services.AddScoped<IWooOrderRepository, WooOrderRepository>();
builder.Services.AddScoped<ISyncLogsRepository, SyncLogsRepository>();
builder.Services.AddScoped<IWooOrderProductRepository, WooOrderProductRepository>();
builder.Services.AddScoped<ISyncService, SyncService>();
builder.Services.AddScoped<IKosePaymentsRepository, KosePaymentsRepository>();
//
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "API v1");
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
