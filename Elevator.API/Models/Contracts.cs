using Elevator.API.Models;

public sealed record TripRequestDto(
    int ElevatorId,
    int? CurrentFloor,
    Direction Direction,
    int SourceFloor,
    int? DestinationFloor
 );

public sealed record AssignElevatorCommand(
    Direction Direction,
    int SourceFloor,
    int? DestinationFloor
);

public sealed record AssignedElevatorResult(
    int ElevatorId,
    string ElevatorName,
    int CurrentFloor,
    Direction Direction,
    int SourceFloor,
    int? DestinationFloor
);

public sealed record AssignedElevatorEvent(
    Guid RequestId,
    int ElevatorId,
    string ElevatorName,
    int CurrentFloor,
    Direction Direction,
    int SourceFloor,
    int? DestinationFloor,
    DateTime AssignedAtUtc
);

public interface ICommandHandler<TCommand, TResult>
{
    Task<TResult?> HandleAsync(TCommand command, CancellationToken ct = default);
}

public interface ICommandDispatcher
{
    Task<TResult?> DispatchAsync<TCommand, TResult>(TCommand command, CancellationToken ct = default);
}
