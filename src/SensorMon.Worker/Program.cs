using Microsoft.EntityFrameworkCore;
using SensorMon.Worker;

// Host.CreateApplicationBuilder gives you config, logging, and DI with no web server.
// This is the Worker Service template's entry point — minimal by design.
var builder = Host.CreateApplicationBuilder(args);

// Bind the "Sensor" config section to SensorOptions (from appsettings.json or env vars).
builder.Services.Configure<SensorOptions>(builder.Configuration.GetSection("Sensor"));

// IHttpClientFactory is the recommended way to get HttpClient instances
// (handles socket reuse / DNS refresh correctly).
builder.Services.AddHttpClient("lhm");

// Register the DbContext. Connection string comes from configuration:
//   ConnectionStrings:Postgres
// In k8s this will be injected from a Secret as an env var:
//   ConnectionStrings__Postgres
builder.Services.AddDbContext<SensorDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

// Register the background worker.
builder.Services.AddHostedService<PollingWorker>();

var host = builder.Build();

// Apply EF Core migrations on startup so a fresh pod creates its schema automatically.
// Fine for a homelab; in bigger setups you'd run migrations as a separate step/job.
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<SensorDbContext>();
    db.Database.Migrate();
}

host.Run();
