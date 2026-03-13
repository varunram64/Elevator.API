namespace Elevator.API.Models
{
    public enum ElevatorServiceStatus
    {
        Active = 1,
        Inactive = 2,
        Blocked = 3
    }

    public enum ServiceLogStatus
    {
        InProgress = 1,
        Completed = 2
    }

    public enum Direction
    {
        Up = 1,
        Down = 2
    }

    public sealed record TripRequestMessage(
        Guid RequestId,
        Direction Direction,
        int SourceFloor,
        int? DestinationFloor,
        DateTime RequestedAtUtc,
        int? CurrentFloor
    );


    public class Elevators
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public ElevatorServiceStatus ServiceStatus { get; set; }
    }

    public class ServiceLog
    {
        public int Id { get; set; }
        public int ElevatorId { get; set; }
        public Direction Direction { get; set; }
        public int SourceFloor { get; set; }
        public int? DestinationFloor { get; set; }
        public ServiceLogStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

}
