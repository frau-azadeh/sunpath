using sunpath.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Services.Interface
{
    public interface ITripCalculatorService
    {
        /// <summary>
        /// محاسبه فاصله بر اساس مختصات جغرافیایی (فرمول Haversine) به کیلومتر
        /// </summary>
        double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2);

        /// <summary>
        /// محاسبه تخمینی سوخت مصرفی بر اساس مسافت طی‌شده و ضریب مصرف خودرو در هر 100 کیلومتر
        /// </summary>
        decimal CalculateFuelConsumptionLiters(double distanceKm, decimal fuelConsumptionPer100Km = 8.5m);

        /// <summary>
        /// محاسبه امتیاز رانندگی ایمن و بهره‌وری راننده (۰ تا ۱۰۰) بر اساس شتاب ناگهانی، سرعت غیرمجاز و زمان درجا
        /// </summary>
        int CalculateEfficiencyScore(int harshBrakingCount, int overSpeedingSeconds, int idlingSeconds, double totalDistanceKm);

        /// <summary>
        /// تحلیل کامل نقاط حرکتی و ژئولوکیشن‌های ثبت‌شده در طول سفر و خروجی متریک‌های سفر
        /// </summary>
        TripAnalysisResult AnalyzeTripTrajectory(IEnumerable<VehicleLocationHistory> trajectoryPoints, decimal standardFuelRate = 8.5m);

        /// <summary>
        /// محاسبه و ذخیره یا به‌روزرسانی نهایی متریک‌های سفر در دیتابیس برای یک مأموریت (Dispatch) مشخص
        /// </summary>
        Task<TripMetricsDto> ComputeAndSaveMissionMetricsAsync(int dispatchId);
    }

    #region DTOs & Return Models

    /// <summary>
    /// خروجی پردازش و تحلیل حرکتی سفر
    /// </summary>
    public class TripAnalysisResult
    {
        public double TotalDistanceKm { get; set; }
        public TimeSpan TotalDuration { get; set; }
        public TimeSpan IdleDuration { get; set; }
        public TimeSpan MovingDuration { get; set; }
        public double MaxSpeedKmh { get; set; }
        public double AverageSpeedKmh { get; set; }
        public decimal EstimatedFuelLiters { get; set; }
        public int EfficiencyScore { get; set; }
        public int StopCount { get; set; }
    }

    /// <summary>
    /// داده‌های مرتبط با جدول TripMetrics جهت انتقال در لایه سرویس و کنترلر
    /// </summary>
    public class TripMetricsDto
    {
        public int Id { get; set; }
        public int DispatchId { get; set; }
        public int DriverId { get; set; }
        public int VehicleId { get; set; }
        public double TotalDistanceKm { get; set; }
        public int TotalDurationMinutes { get; set; }
        public int IdleDurationMinutes { get; set; }
        public decimal FuelConsumedLiters { get; set; }
        public double AverageSpeed { get; set; }
        public double MaxSpeed { get; set; }
        public int EfficiencyScore { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.UtcNow;
    }

    #endregion
}