using SQLDatabaseScriptGenerator.Models;
using SQLDatabaseScriptGenerator.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Settings
builder.Services.Configure<AiSettings>(builder.Configuration.GetSection(AiSettings.SectionName));

// Add services to the container.
builder.Services.AddControllersWithViews();

// Register HTTP Client for Ollama / LLM provider
builder.Services.AddHttpClient<IOllamaService, OllamaService>();

// Register Domain & Core Engine Services
builder.Services.AddScoped<ISqlParserService, SqlParserService>();
builder.Services.AddScoped<ISqlEngineService, SqlEngineService>();
builder.Services.AddSingleton<ISqlTemplateService, SqlTemplateService>();
builder.Services.AddSingleton<ISqlStandardsService, SqlStandardsService>();
builder.Services.AddSingleton<ISqlTranspilerService, SqlTranspilerService>();
builder.Services.AddSingleton<ISchemaVisualizerService, SchemaVisualizerService>();
builder.Services.AddScoped<ILiveDatabaseInspectorService, LiveDatabaseInspectorService>();

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
