using MessageHub.Core;
using MessageHub.Domain;
using MessageHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddMessageHubCore();
builder.Services.AddMessageHubInfrastructure();
builder.Services.AddMessageHubDomain();

var app = builder.Build();

// 初始化 SQLite 資料庫（建立資料表與索引）
await app.Services.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new
{
    status = "ok",
    service = "MessageHub",
    time = DateTimeOffset.UtcNow
}));

app.Run();
