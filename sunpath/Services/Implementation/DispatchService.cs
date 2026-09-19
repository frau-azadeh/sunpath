using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using sunpath.Hubs;
using sunpath.Models;
using sunpath.Models.Dto;
using sunpath.Services.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace sunpath.Services.Implementation
{
    public class DispatchService : IDispatchService
    {
        private readonly string _connectionString;
        private readonly IHubContext<VehicleHub> _vehicleHub;

        private const double MinMovementKm = 0.005;
        private const double MaxMovementKm = 2.0;
        private const double FuelPer100Km = 8.5;
        private const double IdleFuelPerHour = 1.1;

        public DispatchService(
            IConfiguration configuration,
            IHubContext<VehicleHub> vehicleHub)
        {
            _connectionString =
                configuration.GetConnectionString("SunPathConnection");

            _vehicleHub = vehicleHub;
        }

        public async Task<int> CreateAsync(
            CreateDispatchRequest request)
        {
            using (var connection =
                new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =
                    connection.BeginTransaction())
                {
                    try
                    {
                        // -------------------------------------------------
                        // فقط یک مأموریت فعال برای هر خودرو یا راننده
                        // -------------------------------------------------
                        const string conflictSql = @"
SELECT COUNT(1)
FROM Missions
WHERE Status IN (1, 2)
  AND
  (
      VehicleId = @VehicleId
      OR
      (
          @DriverId IS NOT NULL
          AND DriverId = @DriverId
      )
  );";

                        using (var conflict =
                            new SqlCommand(
                                conflictSql,
                                connection,
                                transaction))
                        {
                            conflict.Parameters
                                .Add(
                                    "@VehicleId",
                                    SqlDbType.Int)
                                .Value = request.VehicleId;

                            conflict.Parameters
                                .Add(
                                    "@DriverId",
                                    SqlDbType.Int)
                                .Value =
                                    (object)request.DriverId
                                    ?? DBNull.Value;

                            var conflictCount =
                                Convert.ToInt32(
                                    await conflict
                                        .ExecuteScalarAsync());

                            if (conflictCount > 0)
                            {
                                throw new InvalidOperationException(
                                    "این خودرو یا راننده در حال حاضر مأموریت فعال دارد.");
                            }
                        }

                        // -------------------------------------------------
                        // ایجاد مأموریت
                        // -------------------------------------------------
                        const string insertSql = @"
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
    CreatedAtUtc,
    UpdatedAtUtc
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
    1,
    @Now,
    @Now
);

SELECT CAST(SCOPE_IDENTITY() AS INT);";

                        int id;

                        using (var command =
                            new SqlCommand(
                                insertSql,
                                connection,
                                transaction))
                        {
                            AddDispatchParams(
                                command,
                                request);

                            command.Parameters
                                .Add(
                                    "@Now",
                                    SqlDbType.DateTime2)
                                .Value = DateTime.UtcNow;

                            id = Convert.ToInt32(
                                await command
                                    .ExecuteScalarAsync());
                        }

                        // -------------------------------------------------
                        // اتصال راننده به خودرو + صفر کردن آمار سفر
                        // -------------------------------------------------
                        const string updateVehicleSql = @"
UPDATE Vehicles
SET
    CurrentDriverId = @DriverId,
    FuelConsumedLiters = 0,
    TripDistanceKm = 0,
    TripDurationSeconds = 0,
    StopDurationSeconds = 0
WHERE Id = @VehicleId;";

                        using (var vehicleCommand =
                            new SqlCommand(
                                updateVehicleSql,
                                connection,
                                transaction))
                        {
                            vehicleCommand.Parameters
                                .Add(
                                    "@DriverId",
                                    SqlDbType.Int)
                                .Value =
                                    (object)request.DriverId
                                    ?? DBNull.Value;

                            vehicleCommand.Parameters
                                .Add(
                                    "@VehicleId",
                                    SqlDbType.Int)
                                .Value = request.VehicleId;

                            await vehicleCommand
                                .ExecuteNonQueryAsync();
                        }

                        transaction.Commit();

                        var dispatch =
                            await GetByIdAsync(id);

                        await _vehicleHub.Clients.All
                            .SendAsync(
                                "VehicleUpdated",
                                new
                                {
                                    id =
                                        request.VehicleId,

                                    currentDriverId =
                                        request.DriverId,

                                    activeDispatchId =
                                        id,

                                    activeDispatchDriverId =
                                        request.DriverId,

                                    originAddress =
                                        request.OriginTitle,

                                    originLat =
                                        request.OriginLatitude,

                                    originLng =
                                        request.OriginLongitude,

                                    destinationAddress =
                                        request.DestinationTitle,

                                    destinationLat =
                                        request.DestinationLatitude,

                                    destinationLng =
                                        request.DestinationLongitude,

                                    dispatchStatus =
                                        "Assigned"
                                });

                        await _vehicleHub.Clients.All
                            .SendAsync(
                                "DispatchCreated",
                                dispatch);

                        return id;
                    }
                    catch
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch
                        {
                        }

                        throw;
                    }
                }
            }
        }

        public async Task<bool> UpdateAsync(
            int id,
            UpdateDispatchRequest request)
        {
            var current =
                await GetByIdAsync(id);

            if (current == null)
            {
                return false;
            }

            if (
                current.Status ==
                    DispatchStatus.Completed ||
                current.Status ==
                    DispatchStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    "مأموریت تکمیل یا لغو شده قابل ویرایش نیست.");
            }

            const string sql = @"
UPDATE Missions
SET
    DriverId = @DriverId,
    VehicleId = @VehicleId,
    Title = @Title,
    Description = @Description,
    OriginTitle = @OriginTitle,
    OriginLatitude = @OriginLatitude,
    OriginLongitude = @OriginLongitude,
    DestinationTitle = @DestinationTitle,
    DestinationLatitude = @DestinationLatitude,
    DestinationLongitude = @DestinationLongitude,
    UpdatedAtUtc = @Now
WHERE Id = @Id;";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(sql, connection))
            {
                command.Parameters
                    .Add(
                        "@Id",
                        SqlDbType.Int)
                    .Value = id;

                command.Parameters
                    .Add(
                        "@DriverId",
                        SqlDbType.Int)
                    .Value =
                        (object)request.DriverId
                        ?? DBNull.Value;

                command.Parameters
                    .Add(
                        "@VehicleId",
                        SqlDbType.Int)
                    .Value = request.VehicleId;

                command.Parameters
                    .Add(
                        "@Title",
                        SqlDbType.NVarChar,
                        200)
                    .Value =
                        (object)request.Title
                        ?? DBNull.Value;

                command.Parameters
                    .Add(
                        "@Description",
                        SqlDbType.NVarChar,
                        1000)
                    .Value =
                        (object)request.Description
                        ?? DBNull.Value;

                command.Parameters
                    .Add(
                        "@OriginTitle",
                        SqlDbType.NVarChar,
                        300)
                    .Value =
                        (object)request.OriginTitle
                        ?? DBNull.Value;

                AddNullableDecimalParameter(
                    command,
                    "@OriginLatitude",
                    request.OriginLatitude,
                    9,
                    6);

                AddNullableDecimalParameter(
                    command,
                    "@OriginLongitude",
                    request.OriginLongitude,
                    9,
                    6);

                command.Parameters
                    .Add(
                        "@DestinationTitle",
                        SqlDbType.NVarChar,
                        300)
                    .Value =
                        (object)request.DestinationTitle
                        ?? DBNull.Value;

                AddNullableDecimalParameter(
                    command,
                    "@DestinationLatitude",
                    request.DestinationLatitude,
                    9,
                    6);

                AddNullableDecimalParameter(
                    command,
                    "@DestinationLongitude",
                    request.DestinationLongitude,
                    9,
                    6);

                command.Parameters
                    .Add(
                        "@Now",
                        SqlDbType.DateTime2)
                    .Value = DateTime.UtcNow;

                await connection.OpenAsync();

                var updated =
                    await command.ExecuteNonQueryAsync() > 0;

                if (updated)
                {
                    await _vehicleHub.Clients.All
                        .SendAsync(
                            "DispatchUpdated",
                            await GetByIdAsync(id));
                }

                return updated;
            }
        }

        public async Task<Dispatch> GetByIdAsync(
            int id)
        {
            const string sql = @"
SELECT TOP 1 *
FROM Missions
WHERE Id = @Id;";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(sql, connection))
            {
                command.Parameters
                    .Add(
                        "@Id",
                        SqlDbType.Int)
                    .Value = id;

                await connection.OpenAsync();

                using (var reader =
                    await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return Map(reader);
                    }
                }
            }

            return null;
        }

        public async Task<List<Dispatch>>
            GetAllAsync()
        {
            var result =
                new List<Dispatch>();

            const string sql = @"
SELECT *
FROM Missions
ORDER BY
    CreatedAtUtc DESC,
    Id DESC;";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(sql, connection))
            {
                await connection.OpenAsync();

                using (var reader =
                    await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        result.Add(
                            Map(reader));
                    }
                }
            }

            return result;
        }

        public async Task<Dispatch>
            GetActiveForDriverAsync(
                int driverId)
        {
            const string sql = @"
SELECT TOP 1 *
FROM Missions
WHERE
    DriverId = @DriverId
    AND Status IN (1, 2)
ORDER BY Id DESC;";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(sql, connection))
            {
                command.Parameters
                    .Add(
                        "@DriverId",
                        SqlDbType.Int)
                    .Value = driverId;

                await connection.OpenAsync();

                using (var reader =
                    await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return Map(reader);
                    }
                }
            }

            return null;
        }

        public async Task<bool>
            UpdateStatusAsync(
                int id,
                UpdateDispatchStatusRequest request)
        {
            var status =
                ParseStatus(request.Status);

            var now =
                DateTime.UtcNow;

            const string sql = @"
UPDATE Missions
SET
    Status = @Status,

    StartedAtUtc =
        CASE
            WHEN
                @Status = 2
                AND StartedAtUtc IS NULL
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

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(sql, connection))
            {
                command.Parameters
                    .Add(
                        "@Id",
                        SqlDbType.Int)
                    .Value = id;

                command.Parameters
                    .Add(
                        "@Status",
                        SqlDbType.Int)
                    .Value = status;

                command.Parameters
                    .Add(
                        "@Now",
                        SqlDbType.DateTime2)
                    .Value = now;

                await connection.OpenAsync();

                var changed =
                    await command.ExecuteNonQueryAsync() > 0;

                if (!changed)
                {
                    return false;
                }

                var dispatch =
                    await GetByIdAsync(id);

                await _vehicleHub.Clients.All
                    .SendAsync(
                        "DispatchStatusChanged",
                        dispatch);

                if (
                    status == 3 ||
                    status == 4)
                {
                    await ReleaseVehicleForDispatchAsync(
                        dispatch);
                }

                return true;
            }
        }

        private async Task
            ReleaseVehicleForDispatchAsync(
                Dispatch dispatch)
        {
            if (dispatch == null)
            {
                return;
            }

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(
                    @"
UPDATE Vehicles
SET CurrentDriverId = NULL
WHERE
    Id = @VehicleId
    AND
    (
        CurrentDriverId = @DriverId
        OR
        (
            CurrentDriverId IS NULL
            AND @DriverId IS NULL
        )
    );",
                    connection))
            {
                command.Parameters
                    .Add(
                        "@VehicleId",
                        SqlDbType.Int)
                    .Value =
                        dispatch.VehicleId;

                command.Parameters
                    .Add(
                        "@DriverId",
                        SqlDbType.Int)
                    .Value =
                        (object)dispatch.DriverId
                        ?? DBNull.Value;

                await connection.OpenAsync();

                await command
                    .ExecuteNonQueryAsync();
            }

            await _vehicleHub.Clients.All
                .SendAsync(
                    "VehicleUpdated",
                    new
                    {
                        id =
                            dispatch.VehicleId,

                        currentDriverId =
                            (int?)null,

                        activeDispatchId =
                            (int?)null,

                        activeDispatchDriverId =
                            (int?)null,

                        originAddress =
                            (string)null,

                        originLat =
                            (decimal?)null,

                        originLng =
                            (decimal?)null,

                        destinationAddress =
                            (string)null,

                        destinationLat =
                            (decimal?)null,

                        destinationLng =
                            (decimal?)null,

                        dispatchStatus =
                            (string)null
                    });
        }

        public async Task<bool>
            UpdateVehicleLocationAsync(
                UpdateVehicleLocationRequest request)
        {
            // ---------------------------------------------------------
            // زمان ثبت GPS روی دستگاه
            // ---------------------------------------------------------
            var recordedAt =
                request.RecordedAtUtc
                ?? DateTime.UtcNow;

            // ---------------------------------------------------------
            // زمان دریافت اطلاعات توسط Backend
            // ---------------------------------------------------------
            var receivedAt =
                DateTime.UtcNow;

            double previousLat = 0;
            double previousLng = 0;
            double previousDistance = 0;

            int previousDuration = 0;
            int previousStop = 0;

            DateTime? previousAt = null;

            // ---------------------------------------------------------
            // اطلاعات قبلی خودرو
            // ---------------------------------------------------------
            const string vehicleStateSql = @"
SELECT
    LastLatitude,
    LastLongitude,
    LastUpdateAt,
    FuelConsumedLiters,
    TripDistanceKm,
    TripDurationSeconds,
    StopDurationSeconds
FROM Vehicles
WHERE Id = @VehicleId;";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(
                    vehicleStateSql,
                    connection))
            {
                command.Parameters
                    .Add(
                        "@VehicleId",
                        SqlDbType.Int)
                    .Value =
                        request.VehicleId;

                await connection.OpenAsync();

                using (var reader =
                    await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return false;
                    }

                    if (
                        reader["LastLatitude"] !=
                        DBNull.Value)
                    {
                        previousLat =
                            Convert.ToDouble(
                                reader[
                                    "LastLatitude"
                                ]);
                    }

                    if (
                        reader["LastLongitude"] !=
                        DBNull.Value)
                    {
                        previousLng =
                            Convert.ToDouble(
                                reader[
                                    "LastLongitude"
                                ]);
                    }

                    if (
                        reader["LastUpdateAt"] !=
                        DBNull.Value)
                    {
                        previousAt =
                            Convert.ToDateTime(
                                reader[
                                    "LastUpdateAt"
                                ]);
                    }

                    if (
                        reader["TripDistanceKm"] !=
                        DBNull.Value)
                    {
                        previousDistance =
                            Convert.ToDouble(
                                reader[
                                    "TripDistanceKm"
                                ]);
                    }

                    if (
                        reader["TripDurationSeconds"] !=
                        DBNull.Value)
                    {
                        previousDuration =
                            Convert.ToInt32(
                                reader[
                                    "TripDurationSeconds"
                                ]);
                    }

                    if (
                        reader["StopDurationSeconds"] !=
                        DBNull.Value)
                    {
                        previousStop =
                            Convert.ToInt32(
                                reader[
                                    "StopDurationSeconds"
                                ]);
                    }
                }
            }

            // ---------------------------------------------------------
            // فاصله GPS
            // ---------------------------------------------------------
            var stepDistance = 0.0;

            if (
                previousLat != 0 &&
                previousLng != 0)
            {
                stepDistance =
                    HaversineKm(
                        previousLat,
                        previousLng,
                        Convert.ToDouble(
                            request.Latitude),
                        Convert.ToDouble(
                            request.Longitude));
            }

            // ---------------------------------------------------------
            // حذف نویز کوچک و پرش بزرگ GPS
            // ---------------------------------------------------------
            var validMovement =
                stepDistance >=
                    MinMovementKm &&
                stepDistance <=
                    MaxMovementKm;

            var elapsed = 0.0;

            if (previousAt.HasValue)
            {
                elapsed =
                    Math.Max(
                        0,
                        Math.Min(
                            120,
                            (
                                recordedAt -
                                previousAt.Value
                            ).TotalSeconds));
            }

            var speed =
                Convert.ToDouble(
                    request.Speed ?? 0);

            var newDistance =
                previousDistance +
                (
                    validMovement
                        ? stepDistance
                        : 0
                );

            var newStop =
                previousStop +
                (
                    speed < 3
                        ? (int)Math.Round(
                            elapsed)
                        : 0
                );

            var newDuration =
                previousDuration +
                (int)Math.Round(
                    elapsed);

            var newFuel =
                (
                    newDistance /
                    100.0
                ) *
                FuelPer100Km +
                (
                    newStop /
                    3600.0
                ) *
                IdleFuelPerHour;

            // ---------------------------------------------------------
            // بروزرسانی وضعیت خودرو
            // ---------------------------------------------------------
            const string updateVehicleSql = @"
UPDATE Vehicles
SET
    Latitude = @Lat,
    Longitude = @Lng,
    LastLatitude = @Lat,
    LastLongitude = @Lng,
    Speed = @Speed,
    Heading = @Heading,
    LastUpdate = GETDATE(),
    LastUpdateAt = @RecordedAt,
    FuelConsumedLiters = @Fuel,
    TripDistanceKm = @TripDistance,
    TripDurationSeconds = @TripDuration,
    StopDurationSeconds = @StopDuration
WHERE Id = @VehicleId;";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(
                    updateVehicleSql,
                    connection))
            {
                command.Parameters
                    .Add(
                        "@VehicleId",
                        SqlDbType.Int)
                    .Value =
                        request.VehicleId;

                command.Parameters
                    .Add(
                        "@Lat",
                        SqlDbType.Float)
                    .Value =
                        request.Latitude;

                command.Parameters
                    .Add(
                        "@Lng",
                        SqlDbType.Float)
                    .Value =
                        request.Longitude;

                command.Parameters
                    .Add(
                        "@Speed",
                        SqlDbType.Float)
                    .Value =
                        (object)request.Speed
                        ?? 0d;

                command.Parameters
                    .Add(
                        "@Heading",
                        SqlDbType.Float)
                    .Value =
                        (object)request.Heading
                        ?? 0d;

                command.Parameters
                    .Add(
                        "@RecordedAt",
                        SqlDbType.DateTime2)
                    .Value =
                        recordedAt;

                command.Parameters
                    .Add(
                        "@Fuel",
                        SqlDbType.Float)
                    .Value =
                        newFuel;

                command.Parameters
                    .Add(
                        "@TripDistance",
                        SqlDbType.Float)
                    .Value =
                        newDistance;

                command.Parameters
                    .Add(
                        "@TripDuration",
                        SqlDbType.Int)
                    .Value =
                        newDuration;

                command.Parameters
                    .Add(
                        "@StopDuration",
                        SqlDbType.Int)
                    .Value =
                        newStop;

                await connection.OpenAsync();

                if (
                    await command
                        .ExecuteNonQueryAsync() ==
                    0)
                {
                    return false;
                }
            }

            // ---------------------------------------------------------
            // ذخیره History
            //
            // schema واقعی دیتابیس:
            //
            // RecordedAt
            // ReceivedAt
            //
            // نه RecordedAtUtc
            // ---------------------------------------------------------
            const string historySql = @"
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
    RecordedAt,
    ReceivedAt
)
VALUES
(
    @VehicleId,
    @DriverId,
    @MissionId,
    @Lat,
    @Lng,
    @Accuracy,
    @Speed,
    @Heading,
    @RecordedAt,
    @ReceivedAt
);";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(
                    historySql,
                    connection))
            {
                AddLocationParams(
                    command,
                    request,
                    recordedAt,
                    receivedAt);

                await connection.OpenAsync();

                await command
                    .ExecuteNonQueryAsync();
            }

            // ---------------------------------------------------------
            // SignalR Payload
            // ---------------------------------------------------------
            var payload =
                new
                {
                    vehicleId =
                        request.VehicleId,

                    driverId =
                        request.DriverId,

                    missionId =
                        request.MissionId,

                    latitude =
                        request.Latitude,

                    longitude =
                        request.Longitude,

                    speed =
                        request.Speed ?? 0,

                    heading =
                        request.Heading ?? 0,

                    fuelConsumedLiters =
                        Math.Round(
                            newFuel,
                            2),

                    tripDistanceKm =
                        Math.Round(
                            newDistance,
                            2),

                    tripDurationSeconds =
                        newDuration,

                    stopDurationSeconds =
                        newStop,

                    accuracy =
                        request.Accuracy,

                    recordedAtUtc =
                        recordedAt
                };

            await _vehicleHub.Clients
                .Group("live-map")
                .SendAsync(
                    "VehiclePositionChanged",
                    payload);

            await _vehicleHub.Clients
                .Group(
                    "vehicle-" +
                    request.VehicleId)
                .SendAsync(
                    "VehiclePositionChanged",
                    payload);

            // ---------------------------------------------------------
            // سازگاری با کلاینت‌های قدیمی
            // ---------------------------------------------------------
            await _vehicleHub.Clients
                .Group("live-map")
                .SendAsync(
                    "vehicleLocationUpdated",
                    payload);

            return true;
        }

        public async Task<
            List<DriverRouteHistoryDto>>
            GetDriverRouteHistoryAsync(
                int driverId)
        {
            var result =
                new List<
                    DriverRouteHistoryDto>();

            // ---------------------------------------------------------
            // مأموریت‌های تکمیل‌شده راننده
            // ---------------------------------------------------------
            const string missionsSql = @"
SELECT
    m.Id,
    m.Title,
    m.VehicleId,
    m.OriginTitle,
    m.OriginLatitude,
    m.OriginLongitude,
    m.DestinationTitle,
    m.DestinationLatitude,
    m.DestinationLongitude,
    m.StartedAtUtc,
    m.CompletedAtUtc,
    v.PlateNumber
FROM Missions m
LEFT JOIN Vehicles v
    ON v.Id = m.VehicleId
WHERE
    m.DriverId = @DriverId
    AND m.Status = 3
ORDER BY
    m.CompletedAtUtc DESC,
    m.Id DESC;";

            using (var connection =
                new SqlConnection(_connectionString))
            using (var command =
                new SqlCommand(
                    missionsSql,
                    connection))
            {
                command.Parameters
                    .Add(
                        "@DriverId",
                        SqlDbType.Int)
                    .Value = driverId;

                await connection.OpenAsync();

                using (var reader =
                    await command
                        .ExecuteReaderAsync())
                {
                    while (
                        await reader
                            .ReadAsync())
                    {
                        var item =
                            new DriverRouteHistoryDto
                            {
                                DispatchId =
                                    Convert.ToInt32(
                                        reader[
                                            "Id"
                                        ]),

                                Title =
                                    reader[
                                        "Title"
                                    ] ==
                                    DBNull.Value
                                        ? null
                                        : reader[
                                            "Title"
                                        ].ToString(),

                                VehicleId =
                                    Convert.ToInt32(
                                        reader[
                                            "VehicleId"
                                        ]),

                                VehiclePlate =
                                    reader[
                                        "PlateNumber"
                                    ] ==
                                    DBNull.Value
                                        ? null
                                        : reader[
                                            "PlateNumber"
                                        ].ToString(),

                                OriginTitle =
                                    reader[
                                        "OriginTitle"
                                    ] ==
                                    DBNull.Value
                                        ? null
                                        : reader[
                                            "OriginTitle"
                                        ].ToString(),

                                OriginLatitude =
                                    reader[
                                        "OriginLatitude"
                                    ] ==
                                    DBNull.Value
                                        ? (decimal?)null
                                        : Convert.ToDecimal(
                                            reader[
                                                "OriginLatitude"
                                            ]),

                                OriginLongitude =
                                    reader[
                                        "OriginLongitude"
                                    ] ==
                                    DBNull.Value
                                        ? (decimal?)null
                                        : Convert.ToDecimal(
                                            reader[
                                                "OriginLongitude"
                                            ]),

                                DestinationTitle =
                                    reader[
                                        "DestinationTitle"
                                    ] ==
                                    DBNull.Value
                                        ? null
                                        : reader[
                                            "DestinationTitle"
                                        ].ToString(),

                                DestinationLatitude =
                                    reader[
                                        "DestinationLatitude"
                                    ] ==
                                    DBNull.Value
                                        ? (decimal?)null
                                        : Convert.ToDecimal(
                                            reader[
                                                "DestinationLatitude"
                                            ]),

                                DestinationLongitude =
                                    reader[
                                        "DestinationLongitude"
                                    ] ==
                                    DBNull.Value
                                        ? (decimal?)null
                                        : Convert.ToDecimal(
                                            reader[
                                                "DestinationLongitude"
                                            ]),

                                StartedAtUtc =
                                    reader[
                                        "StartedAtUtc"
                                    ] ==
                                    DBNull.Value
                                        ? (DateTime?)null
                                        : Convert.ToDateTime(
                                            reader[
                                                "StartedAtUtc"
                                            ]),

                                CompletedAtUtc =
                                    reader[
                                        "CompletedAtUtc"
                                    ] ==
                                    DBNull.Value
                                        ? (DateTime?)null
                                        : Convert.ToDateTime(
                                            reader[
                                                "CompletedAtUtc"
                                            ])
                            };

                        result.Add(item);
                    }
                }
            }

            // ---------------------------------------------------------
            // خواندن نقاط GPS هر مأموریت
            // ---------------------------------------------------------
            foreach (var item in result)
            {
                /*
                 * توجه:
                 * نام واقعی ستون دیتابیس RecordedAt است.
                 *
                 * ولی در DTO همچنان RecordedAtUtc داریم
                 * تا قرارداد API فعلی Frontend تغییر نکند.
                 */
                const string historySql = @"
SELECT
    Latitude,
    Longitude,
    Accuracy,
    Speed,
    Heading,
    RecordedAt
FROM VehicleLocationHistory
WHERE
    MissionId = @MissionId
    AND DriverId = @DriverId
ORDER BY
    RecordedAt ASC,
    Id ASC;";

                using (var connection =
                    new SqlConnection(
                        _connectionString))
                using (var command =
                    new SqlCommand(
                        historySql,
                        connection))
                {
                    command.Parameters
                        .Add(
                            "@MissionId",
                            SqlDbType.Int)
                        .Value =
                            item.DispatchId;

                    command.Parameters
                        .Add(
                            "@DriverId",
                            SqlDbType.Int)
                        .Value =
                            driverId;

                    await connection
                        .OpenAsync();

                    using (var reader =
                        await command
                            .ExecuteReaderAsync())
                    {
                        while (
                            await reader
                                .ReadAsync())
                        {
                            item.Points.Add(
                                new DriverRouteHistoryPointDto
                                {
                                    Latitude =
                                        Convert.ToDecimal(
                                            reader[
                                                "Latitude"
                                            ]),

                                    Longitude =
                                        Convert.ToDecimal(
                                            reader[
                                                "Longitude"
                                            ]),

                                    Accuracy =
                                        reader[
                                            "Accuracy"
                                        ] ==
                                        DBNull.Value
                                            ? (decimal?)null
                                            : Convert.ToDecimal(
                                                reader[
                                                    "Accuracy"
                                                ]),

                                    Speed =
                                        reader[
                                            "Speed"
                                        ] ==
                                        DBNull.Value
                                            ? (decimal?)null
                                            : Convert.ToDecimal(
                                                reader[
                                                    "Speed"
                                                ]),

                                    Heading =
                                        reader[
                                            "Heading"
                                        ] ==
                                        DBNull.Value
                                            ? (decimal?)null
                                            : Convert.ToDecimal(
                                                reader[
                                                    "Heading"
                                                ]),

                                    RecordedAtUtc =
                                        Convert.ToDateTime(
                                            reader[
                                                "RecordedAt"
                                            ])
                                });
                        }
                    }
                }

                // -----------------------------------------------------
                // محاسبه مسافت واقعی GPS
                // -----------------------------------------------------
                double distanceKm = 0;

                for (
                    var index = 1;
                    index <
                    item.Points.Count;
                    index++)
                {
                    var previous =
                        item.Points[
                            index - 1];

                    var current =
                        item.Points[
                            index];

                    var step =
                        HaversineKm(
                            Convert.ToDouble(
                                previous.Latitude),

                            Convert.ToDouble(
                                previous.Longitude),

                            Convert.ToDouble(
                                current.Latitude),

                            Convert.ToDouble(
                                current.Longitude));

                    /*
                     * دقیقاً مشابه منطق Live:
                     *
                     * کمتر از 5 متر = نویز GPS
                     * بیشتر از 2 کیلومتر = پرش GPS
                     */
                    if (
                        step >=
                            MinMovementKm &&
                        step <=
                            MaxMovementKm)
                    {
                        distanceKm +=
                            step;
                    }
                }

                item.DistanceKm =
                    Math.Round(
                        distanceKm,
                        2);

                // -----------------------------------------------------
                // مدت زمان مأموریت
                //
                // اولویت با زمان واقعی Mission است.
                // در نبود آن از GPS استفاده می‌کنیم.
                // -----------------------------------------------------
                if (
                    item.StartedAtUtc
                        .HasValue &&
                    item.CompletedAtUtc
                        .HasValue)
                {
                    item.DurationSeconds =
                        Math.Max(
                            0,
                            Convert.ToInt32(
                                (
                                    item.CompletedAtUtc
                                        .Value -
                                    item.StartedAtUtc
                                        .Value
                                ).TotalSeconds));
                }
                else if (
                    item.Points.Count >= 2)
                {
                    item.DurationSeconds =
                        Math.Max(
                            0,
                            Convert.ToInt32(
                                (
                                    item.Points[
                                        item.Points.Count -
                                        1
                                    ].RecordedAtUtc -
                                    item.Points[
                                        0
                                    ].RecordedAtUtc
                                ).TotalSeconds));
                }
                else
                {
                    item.DurationSeconds =
                        0;
                }
            }

            return result;
        }

        public async Task<bool>
            DeleteAsync(
                int id)
        {
            var current =
                await GetByIdAsync(id);

            if (current == null)
            {
                return false;
            }

            using (var connection =
                new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =
                    connection.BeginTransaction())
                {
                    try
                    {
                        // -------------------------------------------------
                        // حذف تاریخچه GPS مأموریت
                        // -------------------------------------------------
                        using (var historyCommand =
                            new SqlCommand(
                                @"
DELETE FROM VehicleLocationHistory
WHERE MissionId = @Id;",
                                connection,
                                transaction))
                        {
                            historyCommand.Parameters
                                .Add(
                                    "@Id",
                                    SqlDbType.Int)
                                .Value = id;

                            await historyCommand
                                .ExecuteNonQueryAsync();
                        }

                        // -------------------------------------------------
                        // حذف مأموریت
                        // -------------------------------------------------
                        bool deleted;

                        using (var command =
                            new SqlCommand(
                                @"
DELETE FROM Missions
WHERE Id = @Id;",
                                connection,
                                transaction))
                        {
                            command.Parameters
                                .Add(
                                    "@Id",
                                    SqlDbType.Int)
                                .Value = id;

                            deleted =
                                await command
                                    .ExecuteNonQueryAsync() >
                                0;
                        }

                        if (deleted)
                        {
                            // ---------------------------------------------
                            // آزاد کردن خودرو
                            // ---------------------------------------------
                            using (var release =
                                new SqlCommand(
                                    @"
UPDATE Vehicles
SET CurrentDriverId = NULL
WHERE
    Id = @VehicleId
    AND
    (
        CurrentDriverId = @DriverId
        OR
        (
            CurrentDriverId IS NULL
            AND @DriverId IS NULL
        )
    );",
                                    connection,
                                    transaction))
                            {
                                release.Parameters
                                    .Add(
                                        "@VehicleId",
                                        SqlDbType.Int)
                                    .Value =
                                        current.VehicleId;

                                release.Parameters
                                    .Add(
                                        "@DriverId",
                                        SqlDbType.Int)
                                    .Value =
                                        (object)current.DriverId
                                        ?? DBNull.Value;

                                await release
                                    .ExecuteNonQueryAsync();
                            }
                        }

                        transaction.Commit();

                        if (deleted)
                        {
                            await _vehicleHub
                                .Clients.All
                                .SendAsync(
                                    "DispatchDeleted",
                                    id);

                            await _vehicleHub
                                .Clients.All
                                .SendAsync(
                                    "VehicleUpdated",
                                    new
                                    {
                                        id =
                                            current.VehicleId,

                                        currentDriverId =
                                            (int?)null,

                                        activeDispatchId =
                                            (int?)null,

                                        activeDispatchDriverId =
                                            (int?)null,

                                        originAddress =
                                            (string)null,

                                        originLat =
                                            (decimal?)null,

                                        originLng =
                                            (decimal?)null,

                                        destinationAddress =
                                            (string)null,

                                        destinationLat =
                                            (decimal?)null,

                                        destinationLng =
                                            (decimal?)null,

                                        dispatchStatus =
                                            (string)null
                                    });
                        }

                        return deleted;
                    }
                    catch
                    {
                        try
                        {
                            transaction.Rollback();
                        }
                        catch
                        {
                        }

                        throw;
                    }
                }
            }
        }

        private static void AddDispatchParams(
            SqlCommand command,
            CreateDispatchRequest request)
        {
            command.Parameters
                .Add(
                    "@DriverId",
                    SqlDbType.Int)
                .Value =
                    (object)request.DriverId
                    ?? DBNull.Value;

            command.Parameters
                .Add(
                    "@VehicleId",
                    SqlDbType.Int)
                .Value =
                    request.VehicleId;

            command.Parameters
                .Add(
                    "@Title",
                    SqlDbType.NVarChar,
                    200)
                .Value =
                    (object)request.Title
                    ?? DBNull.Value;

            command.Parameters
                .Add(
                    "@Description",
                    SqlDbType.NVarChar,
                    1000)
                .Value =
                    (object)request.Description
                    ?? DBNull.Value;

            command.Parameters
                .Add(
                    "@OriginTitle",
                    SqlDbType.NVarChar,
                    300)
                .Value =
                    (object)request.OriginTitle
                    ?? DBNull.Value;

            AddNullableDecimalParameter(
                command,
                "@OriginLatitude",
                request.OriginLatitude,
                9,
                6);

            AddNullableDecimalParameter(
                command,
                "@OriginLongitude",
                request.OriginLongitude,
                9,
                6);

            command.Parameters
                .Add(
                    "@DestinationTitle",
                    SqlDbType.NVarChar,
                    300)
                .Value =
                    (object)request.DestinationTitle
                    ?? DBNull.Value;

            AddNullableDecimalParameter(
                command,
                "@DestinationLatitude",
                request.DestinationLatitude,
                9,
                6);

            AddNullableDecimalParameter(
                command,
                "@DestinationLongitude",
                request.DestinationLongitude,
                9,
                6);
        }

        private static void AddLocationParams(
            SqlCommand command,
            UpdateVehicleLocationRequest request,
            DateTime recordedAt,
            DateTime receivedAt)
        {
            command.Parameters
                .Add(
                    "@VehicleId",
                    SqlDbType.Int)
                .Value =
                    request.VehicleId;

            command.Parameters
                .Add(
                    "@DriverId",
                    SqlDbType.Int)
                .Value =
                    (object)request.DriverId
                    ?? DBNull.Value;

            command.Parameters
                .Add(
                    "@MissionId",
                    SqlDbType.Int)
                .Value =
                    (object)request.MissionId
                    ?? DBNull.Value;

            AddDecimalParameter(
                command,
                "@Lat",
                request.Latitude,
                9,
                6);

            AddDecimalParameter(
                command,
                "@Lng",
                request.Longitude,
                9,
                6);

            AddNullableDecimalParameter(
                command,
                "@Accuracy",
                request.Accuracy,
                8,
                2);

            AddNullableDecimalParameter(
                command,
                "@Speed",
                request.Speed,
                8,
                2);

            AddNullableDecimalParameter(
                command,
                "@Heading",
                request.Heading,
                8,
                2);

            command.Parameters
                .Add(
                    "@RecordedAt",
                    SqlDbType.DateTime2)
                .Value =
                    recordedAt;

            command.Parameters
                .Add(
                    "@ReceivedAt",
                    SqlDbType.DateTime2)
                .Value =
                    receivedAt;
        }

        private static void AddDecimalParameter(
            SqlCommand command,
            string parameterName,
            decimal value,
            byte precision,
            byte scale)
        {
            var parameter =
                command.Parameters.Add(
                    parameterName,
                    SqlDbType.Decimal);

            parameter.Precision =
                precision;

            parameter.Scale =
                scale;

            parameter.Value =
                value;
        }

        private static void AddNullableDecimalParameter(
            SqlCommand command,
            string parameterName,
            decimal? value,
            byte precision,
            byte scale)
        {
            var parameter =
                command.Parameters.Add(
                    parameterName,
                    SqlDbType.Decimal);

            parameter.Precision =
                precision;

            parameter.Scale =
                scale;

            parameter.Value =
                value.HasValue
                    ? (object)value.Value
                    : DBNull.Value;
        }

        private static double HaversineKm(
            double lat1,
            double lon1,
            double lat2,
            double lon2)
        {
            const double radius =
                6371.0;

            var dLat =
                (lat2 - lat1) *
                Math.PI /
                180.0;

            var dLon =
                (lon2 - lon1) *
                Math.PI /
                180.0;

            var lat1Radians =
                lat1 *
                Math.PI /
                180.0;

            var lat2Radians =
                lat2 *
                Math.PI /
                180.0;

            var a =
                Math.Sin(dLat / 2) *
                Math.Sin(dLat / 2) +
                Math.Cos(lat1Radians) *
                Math.Cos(lat2Radians) *
                Math.Sin(dLon / 2) *
                Math.Sin(dLon / 2);

            /*
             * محافظت در برابر خطای بسیار کوچک floating point.
             */
            a =
                Math.Max(
                    0,
                    Math.Min(
                        1,
                        a));

            return
                radius *
                2 *
                Math.Atan2(
                    Math.Sqrt(a),
                    Math.Sqrt(1 - a));
        }

        private static int ParseStatus(
            string status)
        {
            if (
                int.TryParse(
                    status,
                    out var number))
            {
                if (
                    number >= 0 &&
                    number <= 4)
                {
                    return number;
                }

                throw new ArgumentException(
                    "وضعیت مأموریت نامعتبر است.");
            }

            if (
                string.Equals(
                    status,
                    "Pending",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            if (
                string.Equals(
                    status,
                    "Assigned",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 1;
            }

            if (
                string.Equals(
                    status,
                    "InProgress",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    status,
                    "Started",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 2;
            }

            if (
                string.Equals(
                    status,
                    "Completed",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 3;
            }

            if (
                string.Equals(
                    status,
                    "Cancelled",
                    StringComparison.OrdinalIgnoreCase))
            {
                return 4;
            }

            throw new ArgumentException(
                "وضعیت مأموریت نامعتبر است.");
        }

        private static Dispatch Map(
            SqlDataReader reader)
        {
            return new Dispatch
            {
                Id =
                    Convert.ToInt32(
                        reader["Id"]),

                DriverId =
                    reader["DriverId"] ==
                    DBNull.Value
                        ? (int?)null
                        : Convert.ToInt32(
                            reader[
                                "DriverId"
                            ]),

                VehicleId =
                    Convert.ToInt32(
                        reader[
                            "VehicleId"
                        ]),

                Title =
                    reader["Title"] ==
                    DBNull.Value
                        ? null
                        : reader[
                            "Title"
                        ].ToString(),

                Description =
                    reader["Description"] ==
                    DBNull.Value
                        ? null
                        : reader[
                            "Description"
                        ].ToString(),

                OriginTitle =
                    reader["OriginTitle"] ==
                    DBNull.Value
                        ? null
                        : reader[
                            "OriginTitle"
                        ].ToString(),

                OriginLatitude =
                    reader[
                        "OriginLatitude"
                    ] ==
                    DBNull.Value
                        ? (decimal?)null
                        : Convert.ToDecimal(
                            reader[
                                "OriginLatitude"
                            ]),

                OriginLongitude =
                    reader[
                        "OriginLongitude"
                    ] ==
                    DBNull.Value
                        ? (decimal?)null
                        : Convert.ToDecimal(
                            reader[
                                "OriginLongitude"
                            ]),

                DestinationTitle =
                    reader[
                        "DestinationTitle"
                    ] ==
                    DBNull.Value
                        ? null
                        : reader[
                            "DestinationTitle"
                        ].ToString(),

                DestinationLatitude =
                    reader[
                        "DestinationLatitude"
                    ] ==
                    DBNull.Value
                        ? (decimal?)null
                        : Convert.ToDecimal(
                            reader[
                                "DestinationLatitude"
                            ]),

                DestinationLongitude =
                    reader[
                        "DestinationLongitude"
                    ] ==
                    DBNull.Value
                        ? (decimal?)null
                        : Convert.ToDecimal(
                            reader[
                                "DestinationLongitude"
                            ]),

                Status =
                    (DispatchStatus)
                    Convert.ToInt32(
                        reader[
                            "Status"
                        ]),

                StartedAtUtc =
                    reader[
                        "StartedAtUtc"
                    ] ==
                    DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(
                            reader[
                                "StartedAtUtc"
                            ]),

                CompletedAtUtc =
                    reader[
                        "CompletedAtUtc"
                    ] ==
                    DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(
                            reader[
                                "CompletedAtUtc"
                            ]),

                CreatedAtUtc =
                    Convert.ToDateTime(
                        reader[
                            "CreatedAtUtc"
                        ]),

                UpdatedAtUtc =
                    reader[
                        "UpdatedAtUtc"
                    ] ==
                    DBNull.Value
                        ? (DateTime?)null
                        : Convert.ToDateTime(
                            reader[
                                "UpdatedAtUtc"
                            ])
            };
        }
    }
}