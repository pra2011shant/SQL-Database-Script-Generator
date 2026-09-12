using SQLDatabaseScriptGenerator.Models;
using SQLDatabaseScriptGenerator.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Settings
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection(AiSettings.SectionName));

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register HTTP Client for Ollama / LLM provider
builder.Services.AddHttpClient<IOllamaClientService, OllamaClientService>();

// Register Domain & Core Engine Services
builder.Services.AddScoped<ISqlParserService, SqlParserService>();
builder.Services.AddScoped<ISqlEngineService, SqlEngineService>();
builder.Services.AddSingleton<ISqlTemplateService, SqlTemplateService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
