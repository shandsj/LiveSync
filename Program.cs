using LiveSync;

var builder = Host.CreateApplicationBuilder(args);
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
builder.Services.Configure<SyncConfiguration>(builder.Configuration.GetSection("SyncConfiguration"));
builder.Services.AddTransient<FileSyncService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
