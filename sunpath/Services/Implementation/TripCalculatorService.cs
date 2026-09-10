using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Services.Implementation
{
    public interface ITripCalculatorService
    {
        double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2);
        double CalculateFuelConsumption(double distanceKm, double stopDurationMinutes, double avgFuelPer100Km = 8.5);
        double CalculateEfficiencyScore(double totalDurationMinutes, double stopDurationMinutes, double distanceKm);
    }

    public class TripCalculatorService : ITripCalculatorService
    {
        public double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371; // شعاع زمین (کیلومتر)
            var dLat = (lat2 - lat1) * (Math.PI / 180.0);
            var dLon = (lon2 - lon1) * (Math.PI / 180.0);

            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * (Math.PI / 180.0)) * Math.Cos(lat2 * (Math.PI / 180.0)) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return Math.Round(R * c, 2);
        }

        public double CalculateFuelConsumption(double distanceKm, double stopDurationMinutes, double avgFuelPer100Km = 8.5)
        {
            // مصرف حین حرکت
            double drivingFuel = (distanceKm / 100.0) * avgFuelPer100Km;
            // مصرف در حالت درجا (تقریباً ۱.۱ لیتر بر ساعت در توقف‌های روشن)
            double idleFuel = (stopDurationMinutes / 60.0) * 1.1;
            return Math.Round(drivingFuel + idleFuel, 2);
        }

        public double CalculateEfficiencyScore(double totalDurationMinutes, double stopDurationMinutes, double distanceKm)
        {
            if (totalDurationMinutes <= 0 || distanceKm <= 0) return 100.0;

            // نسبت زمان توقف به کل زمان سفر
            double stopRatio = stopDurationMinutes / totalDurationMinutes;
            double score = 100.0 - (stopRatio * 45.0);

            return Math.Max(15.0, Math.Min(100.0, Math.Round(score, 1)));
        }
    }
}