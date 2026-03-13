using Elevator.API.Models;

namespace Elevator.API.Repository
{
    public interface IElevatorRepository
    {
        IReadOnlyList<Elevators> GetElevators();
        IReadOnlyList<ServiceLog> GetLogs();
        void AddLog(ServiceLog log);
        void UpdateLog(ServiceLog log);
        ServiceLog? GetLog(int id);
    }

    public sealed class InMemoryElevatorRepository : IElevatorRepository
    {
        private readonly List<Elevators> _elevators = new();
        private readonly List<ServiceLog> _logs = new();
        private int _logId = 1;

        public InMemoryElevatorRepository()
        {
            _elevators.AddRange(new[]
            {
            new Elevators { Id = 1, Name = "E1", ServiceStatus = ElevatorServiceStatus.Active },
            new Elevators { Id = 2, Name = "E2", ServiceStatus = ElevatorServiceStatus.Active },
            new Elevators { Id = 3, Name = "E3", ServiceStatus = ElevatorServiceStatus.Active }
        });
        }

        public IReadOnlyList<Elevators> GetElevators() => _elevators;

        public IReadOnlyList<ServiceLog> GetLogs() => _logs;

        public void AddLog(ServiceLog log)
        {
            log.Id = _logId++;
            _logs.Add(log);
        }

        public void UpdateLog(ServiceLog log)
        {
            var index = _logs.FindIndex(l => l.Id == log.Id);
            if (index >= 0)
                _logs[index] = log;
        }

        public ServiceLog? GetLog(int id) => _logs.FirstOrDefault(l => l.Id == id);
    }

}
