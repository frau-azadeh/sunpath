using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sunpath.Models;
using sunpath.Models.Dto;
using sunpath.Services.Interface;

namespace sunpath.Services
{
    // One shared hosted instance for all clients. A simulation survives closing the browser.
    public class VehicleSimulationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<VehicleSimulationService> _logger;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
        private readonly Dictionary<int, SimState> _active = new Dictionary<int, SimState>();
        private class SimState
        {
            public Dispatch Mission;
            public List<SimulationPoint> Route;
            public int Next = 1;
            public double Lat, Lng, Speed, Heading;
        }
        public VehicleSimulationService(IServiceScopeFactory scopeFactory, ILogger<VehicleSimulationService> logger)
        { _scopeFactory = scopeFactory; _logger = logger; }

        public async Task<bool> IsRunningAsync(int vehicleId)
        {
            await _gate.WaitAsync();
            try { return _active.ContainsKey(vehicleId); }
            finally { _gate.Release(); }
        }
        public async Task StartMissionAsync(Dispatch mission, double speed, List<SimulationPoint> route)
        {
            await _gate.WaitAsync();
            try
            {
                if (_active.ContainsKey(mission.VehicleId)) throw new InvalidOperationException("شبیه‌سازی این وسیله در حال اجراست.");
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dispatches = scope.ServiceProvider.GetRequiredService<IDispatchService>();
                    if (!await dispatches.UpdateStatusAsync(mission.Id, new UpdateDispatchStatusRequest { Status = "Started" }))
                        throw new InvalidOperationException("شروع مأموریت ناموفق بود.");
                    var state = new SimState { Mission = mission, Route = route, Lat = route[0].Latitude, Lng = route[0].Longitude, Speed = speed };
                    var vehicles = scope.ServiceProvider.GetRequiredService<IVehicleService>();
                    if (!await vehicles.UpdateVehicleStatusAsync(mission.VehicleId, state.Lat, state.Lng, 0, 0)) throw new InvalidOperationException("وسیله پیدا نشد.");
                    if (!await SendAsync(dispatches, state, 0)) throw new InvalidOperationException("ثبت مبدأ ناموفق بود.");
                    _active[mission.VehicleId] = state;
                }
            }
            finally { _gate.Release(); }
        }
        public async Task StopAsync(int vehicleId)
        {
            await _gate.WaitAsync();
            try
            {
                SimState state;
                if (!_active.TryGetValue(vehicleId, out state)) return;
                _active.Remove(vehicleId);
                using (var scope = _scopeFactory.CreateScope())
                    await SendAsync(scope.ServiceProvider.GetRequiredService<IDispatchService>(), state, 0);
            }
            finally { _gate.Release(); }
        }
        private static Task<bool> SendAsync(IDispatchService service, SimState state, double speed)
        {
            return service.UpdateVehicleLocationAsync(new UpdateVehicleLocationRequest {
                VehicleId = state.Mission.VehicleId, DriverId = state.Mission.DriverId, MissionId = state.Mission.Id,
                Latitude = (decimal)state.Lat, Longitude = (decimal)state.Lng,
                Speed = (decimal)speed, Heading = (decimal)state.Heading, Accuracy = 0, RecordedAtUtc = DateTime.UtcNow
            });
        }
        public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
        {
            var a = Math.PI / 180;
            var x = Math.Pow(Math.Sin((lat2 - lat1) * a / 2), 2) + Math.Cos(lat1 * a) * Math.Cos(lat2 * a) * Math.Pow(Math.Sin((lng2 - lng1) * a / 2), 2);
            return 6371 * 2 * Math.Atan2(Math.Sqrt(x), Math.Sqrt(Math.Max(0, 1 - x)));
        }
        private static void Advance(SimState state, double seconds)
        {
            var remaining = state.Speed * seconds / 3600;
            while (state.Next < state.Route.Count)
            {
                var target = state.Route[state.Next];
                var distance = DistanceKm(state.Lat, state.Lng, target.Latitude, target.Longitude);
                if (distance < 0.000001) { state.Lat = target.Latitude; state.Lng = target.Longitude; state.Next++; continue; }
                var radians = Math.PI / 180;
                var delta = (target.Longitude - state.Lng) * radians;
                state.Heading = (Math.Atan2(Math.Sin(delta) * Math.Cos(target.Latitude * radians), Math.Cos(state.Lat * radians) * Math.Sin(target.Latitude * radians) - Math.Sin(state.Lat * radians) * Math.Cos(target.Latitude * radians) * Math.Cos(delta)) / radians + 360) % 360;
                var fraction = Math.Min(1, remaining / distance);
                state.Lat += (target.Latitude - state.Lat) * fraction;
                state.Lng += (target.Longitude - state.Lng) * fraction;
                if (fraction < 1) break;
                remaining -= distance; state.Next++;
            }
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var last = clock.Elapsed.TotalSeconds;
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(1000, stoppingToken);
                    var now = clock.Elapsed.TotalSeconds;
                    var seconds = Math.Min(5, now - last); last = now;
                    await _gate.WaitAsync(stoppingToken);
                    try
                    {
                        foreach (var entry in _active.ToArray())
                        {
                            try
                            {
                                using (var scope = _scopeFactory.CreateScope())
                                {
                                    var service = scope.ServiceProvider.GetRequiredService<IDispatchService>();
                                    var current = await service.GetByIdAsync(entry.Value.Mission.Id);
                                    if (current == null || current.Status != DispatchStatus.Started) { _active.Remove(entry.Key); continue; }
                                    // Route edits stop the old simulation instead of following stale coordinates.
                                    if (current.OriginLatitude != entry.Value.Mission.OriginLatitude || current.OriginLongitude != entry.Value.Mission.OriginLongitude || current.DestinationLatitude != entry.Value.Mission.DestinationLatitude || current.DestinationLongitude != entry.Value.Mission.DestinationLongitude)
                                    { _active.Remove(entry.Key); await SendAsync(service, entry.Value, 0); continue; }
                                    Advance(entry.Value, seconds);
                                    var arrived = entry.Value.Next >= entry.Value.Route.Count;
                                    if (!await SendAsync(service, entry.Value, arrived ? 0 : entry.Value.Speed)) { _active.Remove(entry.Key); continue; }
                                    if (arrived)
                                    {
                                        _active.Remove(entry.Key);
                                        await service.UpdateStatusAsync(current.Id, new UpdateDispatchStatusRequest { Status = "Completed" });
                                    }
                                }
                            }
                            catch (Exception error) { _active.Remove(entry.Key); _logger.LogError(error, "Simulation failed for vehicle {VehicleId}", entry.Key); }
                        }
                    }
                    finally { _gate.Release(); }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        }
    }
    public class SimulationPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }
}
