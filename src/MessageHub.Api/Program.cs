using MessageHub.Core;
using MessageHub.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSwaggerGen();
builder.Services.AddMessageHubInfrastructure();

// Application services (在 Api 層註冊，因 Application 層依賴 Core 而非 Infrastructure)
builder.Services.AddSingleton<ChannelSettingsService>();
builder.Services.AddSingleton<IChannelSettingsService>(sp => sp.GetRequiredService<ChannelSettingsService>());
builder.Services.AddSingleton<ICommonParameterProvider>(sp => sp.GetRequiredService<ChannelSettingsService>());
builder.Services.AddSingleton<UnifiedMessageProcessor>();
builder.Services.AddSingleton<IMessageProcessor>(sp => sp.GetRequiredService<UnifiedMessageProcessor>());

var app = builder.Build();

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
