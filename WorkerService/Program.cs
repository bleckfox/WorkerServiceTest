using WorkerService;

// IHost host = Host.CreateDefaultBuilder(args)
//     .ConfigureServices(services => { services.AddHostedService<Worker>(); })
//     .Build();
//
// host.Run();

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);
// builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();
