using Elevator.API.Interfaces;
using Elevator.API.Models;
using Elevator.API.Producers;
using Elevator.API.Repository;
using Elevator.API.Services;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IElevatorRepository, InMemoryElevatorRepository>(); // or EF Core
builder.Services.AddSingleton<IElevatorStateCache, ElevatorStateCache>();
builder.Services.AddSingleton<IElevatorDispatchService, ElevatorDispatchService>();
builder.Services.AddSingleton<IAssignedEventPublisher, RabbitMqAssignedEventPublisher>();

builder.Services.AddHostedService<ElevatorStateConsumer>(); // listens to elevator positions
builder.Services.AddHostedService<TripRequestConsumer>();   // listens to trip requests

var host = builder.Build();
host.Run();

