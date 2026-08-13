using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sunpath.Models;
using sunpath.Services.Interface;

namespace sunpath.Services
{
    public class VehicleSimulationService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Dictionary<int, SimState> _activeSims = new Dictionary<int, SimState>();
        private readonly Random _random = new Random();

        public VehicleSimulationService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        // ساختار داخلی برای نگه‌داشتن وضعیت هر شبیه‌سازی
        private class SimState
        {
            public double Lat { get; set; }
            public double Lng { get; set; }
            public double Heading { get; set; }
        }

        // شروع شبیه‌سازی برای یک خودرو
        public void Start(int vehicleId, double startLat, double startLng, double heading = 90)
        {
            _activeSims[vehicleId] = new SimState
            {
                Lat = startLat,
                Lng = startLng,
                Heading = heading
            };
        }

        // توقف شبیه‌سازی
        public void Stop(int vehicleId)
        {
            _activeSims.Remove(vehicleId);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                foreach (var entry in new Dictionary<int, SimState>(_activeSims))
                {
                    var vehicleId = entry.Key;
                    var state = entry.Value;

                    // هر تیک، ~۳۰ متر به سمت جهت حرکت اضافه می‌کنیم
                    state.Lng += 0.0003;

                    // سرعت کمی نوسان کنه تا واقعی‌تر دیده بشه
                    double speed = 25 + _random.Next(-3, 4);

                    // هر چند ثانیه یک بار جهت کمی عوض بشه (منحنی دیده بشه)
                    if (_random.Next(5) == 0)
                    {
                        state.Heading = (state.Heading + _random.Next(-15, 16) + 360) % 360;
                    }

                    try
                    {
                        // این متد خودش هم دیتا رو توی DB ذخیره می‌کنه، هم SignalR می‌فرسته
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var vehicleService = scope.ServiceProvider.GetRequiredService<IVehicleService>();
                            await vehicleService.UpdateVehicleStatusAsync(
                                vehicleId,
                                state.Lat,
                                state.Lng,
                                speed,
                                state.Heading);
                        }
                    }
                    catch (Exception)
                    {
                        // اگه خودرو در DB پیدا نشد، شبیه‌سازی رو متوقف کن
                        _activeSims.Remove(vehicleId);
                    }
                }

                try
                {
                    await Task.Delay(1000, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }
    }
}
