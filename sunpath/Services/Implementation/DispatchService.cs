using Microsoft.AspNetCore.SignalR;
using sunpath.Data;
using sunpath.Hubs;
using sunpath.Models;
using sunpath.Models.Dto;
using sunpath.Services.Interface;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;

namespace sunpath.Services.Implementation
{
    public class DispatchService : IDispatchService
    {
        private readonly DbHelper _dbHelper;
        private readonly IHubContext<VehicleHub> _vehicleHubContext;

        public DispatchService(
            DbHelper dbHelper,
            IHubContext<VehicleHub> vehicleHubContext)
        {
            _dbHelper = dbHelper;
            _vehicleHubContext = vehicleHubContext;
        }

        public async Task<int> CreateAsync(CreateDispatchRequest request)
        {
            const string query = @"
                INSERT INTO Missions
                (
                    DriverId,
                    VehicleId,
                    Title,
                    Description,
                    OriginTitle,
                    OriginLatitude,
                    OriginLongitude,
                    DestinationTitle,
                    DestinationLatitude,
                    DestinationLongitude,
                    Status,
                    CreatedAtUtc
                )
                VALUES
                (
                    @DriverId,
                    @VehicleId,
                    @Title,
                    @Description,
                    @OriginTitle,
                    @OriginLatitude,
                    @OriginLongitude,
                    @DestinationTitle,
                    @DestinationLatitude,
                    @DestinationLongitude,
                    @Status,
                    @CreatedAtUtc
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);";

            var parameters = new[]
            {
                new SqlParameter(
                    "@DriverId",
                    (object)request.DriverId ?? DBNull.Value),

                new SqlParameter("@VehicleId", request.VehicleId),

                new SqlParameter(
                    "@Title",
                    (object)request.Title ?? DBNull.Value),

                new SqlParameter(
                    "@Description",
                    (object)request.Description ?? DBNull.Value),

                new SqlParameter(
                    "@OriginTitle",
                    (object)request.OriginTitle ?? DBNull.Value),

                new SqlParameter(
                    "@OriginLatitude",
                    (object)request.OriginLatitude ?? DBNull.Value),

                new SqlParameter(
                    "@OriginLongitude",
                    (object)request.OriginLongitude ?? DBNull.Value),

                new SqlParameter(
                    "@DestinationTitle",
                    (object)request.DestinationTitle ?? DBNull.Value),

                new SqlParameter(
                    "@DestinationLatitude",
                    (object)request.DestinationLatitude ?? DBNull.Value),

                new SqlParameter(
                    "@DestinationLongitude",
                    (object)request.DestinationLongitude ?? DBNull.Value),

                new SqlParameter("@Status", (int)DispatchStatus.Assigned),

                new SqlParameter("@CreatedAtUtc", DateTime.UtcNow)
            };

            var result = await _dbHelper.ExecuteScalarAsync(query, parameters);

            return Convert.ToInt32(result);
        }

        public async Task<Dispatch> GetByIdAsync(int id)
        {
            const string query = @"
                SELECT TOP 1 *
                FROM Missions
                WHERE Id = @Id;";

            var rows = await _dbHelper.ExecuteQueryAsync(
                query,
                new SqlParameter("@Id", id));

            if (rows == null || rows.Count == 0)
            {
                return null;
            }

            return MapDispatch(rows[0]);
        }

        public async Task<List<Dispatch>> GetAllAsync()
        {
            const string query = @"
                SELECT *
                FROM Missions
                ORDER BY CreatedAtUtc DESC, Id DESC;";

            var rows = await _dbHelper.ExecuteQueryAsync(query);

            var dispatches = new List<Dispatch>();

            foreach (var row in rows)
            {
                dispatches.Add(MapDispatch(row));
            }

            return dispatches;
        }

        public async Task<bool> UpdateStatusAsync(
            int id,
            UpdateDispatchStatusRequest request)
        {
            var now = DateTime.UtcNow;
            var statusValue = ParseDispatchStatus(request.Status);

            const string query = @"
                UPDATE Missions
                SET
                    Status = @Status,
                    StartedAtUtc =
                        CASE
                            WHEN @Status = 2 AND StartedAtUtc IS NULL
                            THEN @Now
                            ELSE StartedAtUtc
                        END,
                    CompletedAtUtc =
                        CASE
                            WHEN @Status = 3
                            THEN @Now
                            ELSE CompletedAtUtc
                        END,
                    UpdatedAtUtc = @Now
                WHERE Id = @Id;";

            var parameters = new[]
            {
                new SqlParameter("@Id", id),
                new SqlParameter("@Status", statusValue),
                new SqlParameter("@Now", now)
            };

            var affectedRows = await _dbHelper.ExecuteNonQueryAsync(
                query,
                parameters);

            return affectedRows > 0;
        }

        public async Task<bool> UpdateVehicleLocationAsync(
            UpdateVehicleLocationRequest request)
        {
            var recordedAtUtc = request.RecordedAtUtc ?? DateTime.UtcNow;

            const string updateVehicleQuery = @"
                UPDATE Vehicles
                SET
                    LastLatitude = @Latitude,
                    LastLongitude = @Longitude,
                    LastLocationUpdatedAtUtc = @RecordedAtUtc
                WHERE Id = @VehicleId;";

            var updateVehicleParameters = new[]
            {
                new SqlParameter("@VehicleId", request.VehicleId),
                new SqlParameter("@Latitude", request.Latitude),
                new SqlParameter("@Longitude", request.Longitude),
                new SqlParameter("@RecordedAtUtc", recordedAtUtc)
            };

            var updatedRows = await _dbHelper.ExecuteNonQueryAsync(
                updateVehicleQuery,
                updateVehicleParameters);

            if (updatedRows == 0)
            {
                return false;
            }

            const string insertHistoryQuery = @"
                INSERT INTO VehicleLocationHistory
                (
                    VehicleId,
                    DriverId,
                    MissionId,
                    Latitude,
                    Longitude,
                    Accuracy,
                    Speed,
                    Heading,
                    RecordedAtUtc
                )
                VALUES
                (
                    @VehicleId,
                    @DriverId,
                    @MissionId,
                    @Latitude,
                    @Longitude,
                    @Accuracy,
                    @Speed,
                    @Heading,
                    @RecordedAtUtc
                );";

            var insertHistoryParameters = new[]
            {
                new SqlParameter("@VehicleId", request.VehicleId),

                new SqlParameter(
                    "@DriverId",
                    (object)request.DriverId ?? DBNull.Value),

                new SqlParameter(
                    "@MissionId",
                    (object)request.MissionId ?? DBNull.Value),

                new SqlParameter("@Latitude", request.Latitude),
                new SqlParameter("@Longitude", request.Longitude),

                new SqlParameter(
                    "@Accuracy",
                    (object)request.Accuracy ?? DBNull.Value),

                new SqlParameter(
                    "@Speed",
                    (object)request.Speed ?? DBNull.Value),

                new SqlParameter(
                    "@Heading",
                    (object)request.Heading ?? DBNull.Value),

                new SqlParameter("@RecordedAtUtc", recordedAtUtc)
            };

            await _dbHelper.ExecuteNonQueryAsync(
                insertHistoryQuery,
                insertHistoryParameters);

            var locationPayload = new
            {
                vehicleId = request.VehicleId,
                driverId = request.DriverId,
                missionId = request.MissionId,
                latitude = request.Latitude,
                longitude = request.Longitude,
                accuracy = request.Accuracy,
                speed = request.Speed,
                heading = request.Heading,
                recordedAtUtc = recordedAtUtc
            };

            await _vehicleHubContext.Clients
                .Group("live-map")
                .SendAsync("vehicleLocationUpdated", locationPayload);

            await _vehicleHubContext.Clients
                .Group("vehicle-" + request.VehicleId)
                .SendAsync("vehicleLocationUpdated", locationPayload);

            return true;
        }

        private static int ParseDispatchStatus(string status)
        {
            if (int.TryParse(status, out var intVal))
            {
                return intVal;
            }

            if (Enum.TryParse<DispatchStatus>(
                status,
                true,
                out var parsedStatus))
            {
                return (int)parsedStatus;
            }

            throw new ArgumentException(
                "Invalid dispatch status value.",
                nameof(status));
        }

        /// <summary>
        /// خروجی DbHelper از نوع Dictionary است.
        /// این متد، ستون‌های جدول Missions را به پراپرتی‌های همنام
        /// در مدل Dispatch نگاشت می‌کند.
        /// </summary>
        private static Dispatch MapDispatch(
            Dictionary<string, object> row)
        {
            var dispatch = new Dispatch();

            var properties = typeof(Dispatch).GetProperties(
                BindingFlags.Instance | BindingFlags.Public);

            foreach (var property in properties)
            {
                if (!property.CanWrite)
                {
                    continue;
                }

                object value;

                if (!TryGetValueIgnoreCase(
                    row,
                    property.Name,
                    out value))
                {
                    continue;
                }

                if (value == null || value == DBNull.Value)
                {
                    SetNullValue(dispatch, property);
                    continue;
                }

                try
                {
                    var convertedValue = ConvertValue(
                        value,
                        property.PropertyType);

                    property.SetValue(dispatch, convertedValue);
                }
                catch
                {
                    /*
                     * اگر نوع ستونی در دیتابیس با پراپرتی مدل هم‌خوانی
                     * نداشته باشد، همان پراپرتی مقدار پیش‌فرضش را نگه می‌دارد.
                     * این کار مانع fail شدن کل API به‌خاطر یک ستون می‌شود.
                     */
                }
            }

            return dispatch;
        }

        private static bool TryGetValueIgnoreCase(
            Dictionary<string, object> row,
            string key,
            out object value)
        {
            foreach (var item in row)
            {
                if (string.Equals(
                    item.Key,
                    key,
                    StringComparison.OrdinalIgnoreCase))
                {
                    value = item.Value;
                    return true;
                }
            }

            value = null;
            return false;
        }

        private static void SetNullValue(
            Dispatch dispatch,
            PropertyInfo property)
        {
            var propertyType = property.PropertyType;

            var isNullableValueType =
                Nullable.GetUnderlyingType(propertyType) != null;

            if (!propertyType.IsValueType || isNullableValueType)
            {
                property.SetValue(dispatch, null);
            }
        }
        public async Task<bool> DeleteAsync(int id)
        {
            const string deleteLocationHistoryQuery = @"
        DELETE FROM VehicleLocationHistory
        WHERE MissionId = @Id;";

            const string deleteMissionQuery = @"
        DELETE FROM Missions
        WHERE Id = @Id;";

            var parameters = new[]
            {
        new SqlParameter("@Id", id)
    };

            /*
             * ابتدا تاریخچه‌ی موقعیت‌های مرتبط را پاک می‌کنیم؛
             * چون VehicleLocationHistory.MissionId ممکن است Foreign Key باشد.
             */
            await _dbHelper.ExecuteNonQueryAsync(
                deleteLocationHistoryQuery,
                parameters);

            var affectedRows = await _dbHelper.ExecuteNonQueryAsync(
                deleteMissionQuery,
                new SqlParameter("@Id", id));

            return affectedRows > 0;
        }

        private static object ConvertValue(
            object value,
            Type propertyType)
        {
            var underlyingType =
                Nullable.GetUnderlyingType(propertyType) ?? propertyType;

            if (underlyingType.IsEnum)
            {
                if (value is string)
                {
                    return Enum.Parse(
                        underlyingType,
                        value.ToString(),
                        true);
                }

                var enumBaseType =
                    Enum.GetUnderlyingType(underlyingType);

                var numericValue = Convert.ChangeType(
                    value,
                    enumBaseType,
                    CultureInfo.InvariantCulture);

                return Enum.ToObject(underlyingType, numericValue);
            }

            if (underlyingType == typeof(Guid))
            {
                return value is Guid
                    ? value
                    : Guid.Parse(value.ToString());
            }

            if (underlyingType == typeof(DateTime))
            {
                return value is DateTime
                    ? value
                    : Convert.ToDateTime(
                        value,
                        CultureInfo.InvariantCulture);
            }

            if (underlyingType == typeof(string))
            {
                return Convert.ToString(
                    value,
                    CultureInfo.InvariantCulture);
            }

            if (underlyingType.IsInstanceOfType(value))
            {
                return value;
            }

            return Convert.ChangeType(
                value,
                underlyingType,
                CultureInfo.InvariantCulture);
        }
    }
}
