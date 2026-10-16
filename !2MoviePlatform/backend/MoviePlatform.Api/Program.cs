using Microsoft.EntityFrameworkCore;
using MoviePlatform.Api;
using MoviePlatform.Application;
using MoviePlatform.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Загружаем конфигурацию приложения и переменные окружения.
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables();

// Настройки Vibix.
builder.Services.Configure<VibixOptions>(opts =>
{
    opts.BaseUrl =
        builder.Configuration["VIBIX_BASE_URL"]
        ?? builder.Configuration["Vibix:BaseUrl"]
        ?? "https://vibix.org";

    opts.ApiToken =
        builder.Configuration["VIBIX_API_TOKEN"]
        ?? builder.Configuration["Vibix:ApiToken"]
        ?? "";

    opts.AdminKey =
        builder.Configuration["ADMIN_KEY"]
        ?? builder.Configuration["Vibix:AdminKey"]
        ?? "";

    opts.AutoSync = bool.TryParse(
        builder.Configuration["VIBIX_AUTOSYNC"]
        ?? builder.Configuration["Vibix:AutoSync"],
        out var autoSync) && autoSync;

    opts.SyncIntervalMinutes = int.TryParse(
        builder.Configuration["VIBIX_SYNC_INTERVAL_MINUTES"]
        ?? builder.Configuration["Vibix:SyncIntervalMinutes"],
        out var intervalMinutes)
            ? intervalMinutes
            : 360;

    opts.PageSize = int.TryParse(
        builder.Configuration["VIBIX_PAGE_SIZE"]
        ?? builder.Configuration["Vibix:PageSize"],
        out var pageSize)
            ? pageSize
            : 50;
});

// Строка подключения к PostgreSQL.
var connectionString =
    builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["ConnectionStrings__Default"]
    ?? BuildFromDbEnvVars(builder.Configuration)
    ?? throw new InvalidOperationException(
        "Не задана строка подключения к PostgreSQL. " +
        "Настройте ConnectionStrings:Default или переменные DB_HOST, DB_NAME и DB_PASSWORD.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// HTTP-клиент и сервисы Vibix.
builder.Services.AddHttpClient<VibixCatalogService>();
builder.Services.AddScoped<VibixCatalogService>();
builder.Services.AddScoped<VibixSyncService>();
builder.Services.AddHostedService<VibixBackgroundSync>();

// MVC и Swagger.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS.
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod()));

var app = builder.Build();

// Применение миграций и проверка структуры БД.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var log = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        var pendingMigrations =
            (await db.Database.GetPendingMigrationsAsync()).ToList();

        if (pendingMigrations.Count > 0)
        {
            log.LogInformation(
                "Применяю {Count} миграций: {List}",
                pendingMigrations.Count,
                string.Join(", ", pendingMigrations));
        }
        else
        {
            log.LogInformation("Все миграции уже применены.");
        }

        await db.Database.MigrateAsync();

        // Проверяем наличие Movies.IframeVideoId.
        // Для SqlQueryRaw<int> EF Core ожидает столбец Value.
        var hasColumn = await db.Database
            .SqlQueryRaw<int>(
                """
                SELECT COUNT(*)::int AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = 'Movies'
                  AND column_name = 'IframeVideoId'
                """)
            .FirstOrDefaultAsync();

        log.LogInformation(
            "Проверка схемы: Movies.IframeVideoId существует? {Has}",
            hasColumn > 0);
    }
    catch (Exception ex)
    {
        log.LogError(
            ex,
            "Не удалось применить миграции или проверить схему базы данных.");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();

static string? BuildFromDbEnvVars(IConfiguration cfg)
{
    var host = cfg["DB_HOST"];
    var port = cfg["DB_PORT"];
    var name = cfg["DB_NAME"];
    var user = cfg["DB_USER"];
    var pass = cfg["DB_PASSWORD"];

    if (string.IsNullOrWhiteSpace(host)
        || string.IsNullOrWhiteSpace(name))
    {
        return null;
    }

    return $"Host={host};" +
           $"Port={(string.IsNullOrWhiteSpace(port) ? "5432" : port)};" +
           $"Database={name};" +
           $"Username={(string.IsNullOrWhiteSpace(user) ? "postgres" : user)};" +
           $"Password={pass}";
}
