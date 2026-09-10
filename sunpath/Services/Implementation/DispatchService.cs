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

        public DispatchService(IConfiguration configuration, IHubContext<VehicleHub> vehicleHub)
        {
            _connectionString = configuration.GetConnectionString("SunPathConnection");
            _vehicleHub = vehicleHub;
        }

        public async Task<int> CreateAsync(CreateDispatchRequest request)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        // فقط یک مأموریت فعال برای هر خودرو و راننده مجاز است.
                        const string conflictSql = @"
                            SELECT COUNT(1) FROM Missions
                            WHERE Status IN (1,2)
                              AND (VehicleId=@VehicleId OR (@DriverId IS NOT NULL AND DriverId=@DriverId));";
                        using (var conflict = new SqlCommand(conflictSql, connection, transaction))
                        {
                            conflict.Parameters.Add("@VehicleId", SqlDbType.Int).Value = request.VehicleId;
                            conflict.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)request.DriverId ?? DBNull.Value;
                            if (Convert.ToInt32(await conflict.ExecuteScalarAsync()) > 0)
                                throw new InvalidOperationException("این خودرو یا راننده در حال حاضر مأموریت فعال دارد.");
                        }

                        const string insert = @"
                            INSERT INTO Missions
                            (DriverId, VehicleId, Title, Description, OriginTitle, OriginLatitude, OriginLongitude,
                             DestinationTitle, DestinationLatitude, DestinationLongitude, Status, CreatedAtUtc, UpdatedAtUtc)
                            VALUES
                            (@DriverId,@VehicleId,@Title,@Description,@OriginTitle,@OriginLatitude,@OriginLongitude,
                             @DestinationTitle,@DestinationLatitude,@DestinationLongitude,1,@Now,@Now);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);";
                        using (var command = new SqlCommand(insert, connection, transaction))
                        {
                            AddDispatchParams(command, request);
                            command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                            var id = Convert.ToInt32(await command.ExecuteScalarAsync());

                            const string updateVehicle = @"
                                UPDATE Vehicles SET CurrentDriverId=@DriverId, FuelConsumedLiters=0, TripDistanceKm=0, TripDurationSeconds=0, StopDurationSeconds=0
                                WHERE Id=@VehicleId;";
                            using (var vehicleCommand = new SqlCommand(updateVehicle, connection, transaction))
                            {
                                vehicleCommand.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)request.DriverId ?? DBNull.Value;
                                vehicleCommand.Parameters.Add("@VehicleId", SqlDbType.Int).Value = request.VehicleId;
                                await vehicleCommand.ExecuteNonQueryAsync();
                            }

                            transaction.Commit();

                            var dispatch = await GetByIdAsync(id);
                            await _vehicleHub.Clients.All.SendAsync("VehicleUpdated", new
                            {
                                id = request.VehicleId,
                                currentDriverId = request.DriverId,
                                activeDispatchId = id,
                                activeDispatchDriverId = request.DriverId,
                                originAddress = request.OriginTitle,
                                originLat = request.OriginLatitude,
                                originLng = request.OriginLongitude,
                                destinationAddress = request.DestinationTitle,
                                destinationLat = request.DestinationLatitude,
                                destinationLng = request.DestinationLongitude,
                                dispatchStatus = "Assigned"
                            });
                            await _vehicleHub.Clients.All.SendAsync("DispatchCreated", dispatch);
                            return id;
                        }
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { }
                        throw;
                    }
                }
            }
        }

        public async Task<bool> UpdateAsync(int id, UpdateDispatchRequest request)
        {
            var current = await GetByIdAsync(id);
            if (current == null) return false;
            if (current.Status == DispatchStatus.Completed || current.Status == DispatchStatus.Cancelled)
                throw new InvalidOperationException("مأموریت تکمیل یا لغو شده قابل ویرایش نیست.");

            const string sql = @"
                UPDATE Missions SET DriverId=@DriverId, VehicleId=@VehicleId, Title=@Title,
                    Description=@Description, OriginTitle=@OriginTitle, OriginLatitude=@OriginLatitude,
                    OriginLongitude=@OriginLongitude, DestinationTitle=@DestinationTitle,
                    DestinationLatitude=@DestinationLatitude, DestinationLongitude=@DestinationLongitude,
                    UpdatedAtUtc=@Now WHERE Id=@Id";
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)request.DriverId ?? DBNull.Value;
                command.Parameters.Add("@VehicleId", SqlDbType.Int).Value = request.VehicleId;
                command.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = (object)request.Title ?? DBNull.Value;
                command.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value = (object)request.Description ?? DBNull.Value;
                command.Parameters.Add("@OriginTitle", SqlDbType.NVarChar, 300).Value = (object)request.OriginTitle ?? DBNull.Value;
                command.Parameters.Add("@OriginLatitude", SqlDbType.Decimal).Value = (object)request.OriginLatitude ?? DBNull.Value;
                command.Parameters.Add("@OriginLongitude", SqlDbType.Decimal).Value = (object)request.OriginLongitude ?? DBNull.Value;
                command.Parameters.Add("@DestinationTitle", SqlDbType.NVarChar, 300).Value = (object)request.DestinationTitle ?? DBNull.Value;
                command.Parameters.Add("@DestinationLatitude", SqlDbType.Decimal).Value = (object)request.DestinationLatitude ?? DBNull.Value;
                command.Parameters.Add("@DestinationLongitude", SqlDbType.Decimal).Value = (object)request.DestinationLongitude ?? DBNull.Value;
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = DateTime.UtcNow;
                await connection.OpenAsync();
                var ok = await command.ExecuteNonQueryAsync() > 0;
                if (ok) await _vehicleHub.Clients.All.SendAsync("DispatchUpdated", await GetByIdAsync(id));
                return ok;
            }
        }

        public async Task<Dispatch> GetByIdAsync(int id)
        {
            const string sql = "SELECT TOP 1 * FROM Missions WHERE Id=@Id";
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                    if (await reader.ReadAsync()) return Map(reader);
            }
            return null;
        }

        public async Task<List<Dispatch>> GetAllAsync()
        {
            var result = new List<Dispatch>();
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand("SELECT * FROM Missions ORDER BY CreatedAtUtc DESC, Id DESC", connection))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) result.Add(Map(reader));
            }
            return result;
        }

        public async Task<Dispatch> GetActiveForDriverAsync(int driverId)
        {
            const string sql = @"
                SELECT TOP 1 * FROM Missions
                WHERE DriverId=@DriverId AND Status IN (1,2)
                ORDER BY Id DESC";
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@DriverId", SqlDbType.Int).Value = driverId;
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                    if (await reader.ReadAsync()) return Map(reader);
            }
            return null;
        }

        public async Task<bool> UpdateStatusAsync(int id, UpdateDispatchStatusRequest request)
        {
            var status = ParseStatus(request.Status);
            var now = DateTime.UtcNow;
            const string sql = @"
                UPDATE Missions SET Status=@Status,
                    StartedAtUtc=CASE WHEN @Status=2 AND StartedAtUtc IS NULL THEN @Now ELSE StartedAtUtc END,
                    CompletedAtUtc=CASE WHEN @Status=3 THEN @Now ELSE CompletedAtUtc END,
                    UpdatedAtUtc=@Now WHERE Id=@Id";
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@Status", SqlDbType.Int).Value = status;
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now;
                await connection.OpenAsync();
                var changed = await command.ExecuteNonQueryAsync() > 0;
                if (changed)
                {
                    var dispatch = await GetByIdAsync(id);
                    await _vehicleHub.Clients.All.SendAsync("DispatchStatusChanged", dispatch);
                    if (status == 3 || status == 4)
                    {
                        await ReleaseVehicleForDispatchAsync(dispatch);
                    }
                }
                return changed;
            }
        }

        private async Task ReleaseVehicleForDispatchAsync(Dispatch dispatch)
        {
            if (dispatch == null) return;
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(
                "UPDATE Vehicles SET CurrentDriverId=NULL WHERE Id=@VehicleId AND CurrentDriverId=@DriverId", connection))
            {
                command.Parameters.Add("@VehicleId", SqlDbType.Int).Value = dispatch.VehicleId;
                command.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)dispatch.DriverId ?? DBNull.Value;
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }
            await _vehicleHub.Clients.All.SendAsync("VehicleUpdated", new
            {
                id = dispatch.VehicleId,
                currentDriverId = (int?)null,
                activeDispatchId = (int?)null,
                activeDispatchDriverId = (int?)null,
                originAddress = (string)null,
                originLat = (decimal?)null,
                originLng = (decimal?)null,
                destinationAddress = (string)null,
                destinationLat = (decimal?)null,
                destinationLng = (decimal?)null,
                dispatchStatus = (string)null
            });
        }

        public async Task<bool> UpdateVehicleLocationAsync(UpdateVehicleLocationRequest request)
        {
            var at = request.RecordedAtUtc ?? DateTime.UtcNow;
            double previousLat = 0, previousLng = 0, previousFuel = 0, previousDistance = 0;
            int previousDuration = 0, previousStop = 0;
            DateTime? previousAt = null;

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"SELECT LastLatitude, LastLongitude, LastUpdateAt, FuelConsumedLiters, TripDistanceKm, TripDurationSeconds, StopDurationSeconds FROM Vehicles WHERE Id=@VehicleId", connection))
            {
                command.Parameters.Add("@VehicleId", SqlDbType.Int).Value = request.VehicleId;
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return false;
                    if (reader["LastLatitude"] != DBNull.Value) previousLat = Convert.ToDouble(reader["LastLatitude"]);
                    if (reader["LastLongitude"] != DBNull.Value) previousLng = Convert.ToDouble(reader["LastLongitude"]);
                    if (reader["LastUpdateAt"] != DBNull.Value) previousAt = Convert.ToDateTime(reader["LastUpdateAt"]);
                    if (reader["FuelConsumedLiters"] != DBNull.Value) previousFuel = Convert.ToDouble(reader["FuelConsumedLiters"]);
                    if (reader["TripDistanceKm"] != DBNull.Value) previousDistance = Convert.ToDouble(reader["TripDistanceKm"]);
                    if (reader["TripDurationSeconds"] != DBNull.Value) previousDuration = Convert.ToInt32(reader["TripDurationSeconds"]);
                    if (reader["StopDurationSeconds"] != DBNull.Value) previousStop = Convert.ToInt32(reader["StopDurationSeconds"]);
                }
            }

            var stepDistance = previousLat != 0 && previousLng != 0
                ? HaversineKm(previousLat, previousLng, (double)request.Latitude, (double)request.Longitude) : 0;
            if (stepDistance > 2) stepDistance = 0;
            var elapsed = previousAt.HasValue ? Math.Max(0, Math.Min(120, (at - previousAt.Value).TotalSeconds)) : 0;
            var speed = Convert.ToDouble(request.Speed ?? 0);
            var newDistance = previousDistance + (stepDistance > 0.005 ? stepDistance : 0);
            var newStop = previousStop + (speed < 3 ? (int)Math.Round(elapsed) : 0);
            var newDuration = previousDuration + (int)Math.Round(elapsed);
            var newFuel = (newDistance / 100.0) * 8.5 + (newStop / 3600.0) * 1.1;

            const string update = @"
                UPDATE Vehicles SET Latitude=@Lat, Longitude=@Lng, LastLatitude=@Lat,
                    LastLongitude=@Lng, Speed=@Speed, Heading=@Heading,
                    LastUpdate=GETDATE(), LastUpdateAt=@RecordedAt,
                    FuelConsumedLiters=@Fuel, TripDistanceKm=@TripDistance,
                    TripDurationSeconds=@TripDuration, StopDurationSeconds=@StopDuration
                WHERE Id=@VehicleId";
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(update, connection))
            {
                command.Parameters.Add("@VehicleId", SqlDbType.Int).Value = request.VehicleId;
                command.Parameters.Add("@Lat", SqlDbType.Float).Value = request.Latitude;
                command.Parameters.Add("@Lng", SqlDbType.Float).Value = request.Longitude;
                command.Parameters.Add("@Speed", SqlDbType.Float).Value = (object)request.Speed ?? 0d;
                command.Parameters.Add("@Heading", SqlDbType.Float).Value = (object)request.Heading ?? 0d;
                command.Parameters.Add("@RecordedAt", SqlDbType.DateTime2).Value = at;
                command.Parameters.Add("@Fuel", SqlDbType.Float).Value = newFuel;
                command.Parameters.Add("@TripDistance", SqlDbType.Float).Value = newDistance;
                command.Parameters.Add("@TripDuration", SqlDbType.Int).Value = newDuration;
                command.Parameters.Add("@StopDuration", SqlDbType.Int).Value = newStop;
                await connection.OpenAsync();
                if (await command.ExecuteNonQueryAsync() == 0) return false;
            }

            const string history = @"
                INSERT INTO VehicleLocationHistory
                (VehicleId,DriverId,MissionId,Latitude,Longitude,Accuracy,Speed,Heading,RecordedAtUtc)
                VALUES (@VehicleId,@DriverId,@MissionId,@Lat,@Lng,@Accuracy,@Speed,@Heading,@At)";
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(history, connection))
            {
                AddLocationParams(command, request, at);
                await connection.OpenAsync();
                await command.ExecuteNonQueryAsync();
            }

            var payload = new
            {
                vehicleId = request.VehicleId, driverId = request.DriverId, missionId = request.MissionId,
                latitude = request.Latitude, longitude = request.Longitude,
                speed = request.Speed ?? 0, heading = request.Heading ?? 0,
                fuelConsumedLiters = Math.Round(newFuel, 2), tripDistanceKm = Math.Round(newDistance, 2),
                tripDurationSeconds = newDuration, stopDurationSeconds = newStop,
                accuracy = request.Accuracy, recordedAtUtc = at
            };

            await _vehicleHub.Clients.Group("live-map").SendAsync("VehiclePositionChanged", payload);
            await _vehicleHub.Clients.Group("vehicle-" + request.VehicleId).SendAsync("VehiclePositionChanged", payload);
            // سازگاری با کلاینت‌های قبلی.
            await _vehicleHub.Clients.Group("live-map").SendAsync("vehicleLocationUpdated", payload);
            return true;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var current = await GetByIdAsync(id);
            if (current == null) return false;

            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var tx = connection.BeginTransaction())
                {
                    try
                    {
                        using (var history = new SqlCommand("DELETE FROM VehicleLocationHistory WHERE MissionId=@Id", connection, tx))
                        {
                            history.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                            await history.ExecuteNonQueryAsync();
                        }
                        using (var command = new SqlCommand("DELETE FROM Missions WHERE Id=@Id", connection, tx))
                        {
                            command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                            var ok = await command.ExecuteNonQueryAsync() > 0;
                            if (ok)
                            {
                                using (var release = new SqlCommand(
                                    "UPDATE Vehicles SET CurrentDriverId=NULL WHERE Id=@VehicleId AND CurrentDriverId=@DriverId",
                                    connection, tx))
                                {
                                    release.Parameters.Add("@VehicleId", SqlDbType.Int).Value = current.VehicleId;
                                    release.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)current.DriverId ?? DBNull.Value;
                                    await release.ExecuteNonQueryAsync();
                                }
                            }
                            tx.Commit();
                            if (ok)
                                await _vehicleHub.Clients.All.SendAsync("DispatchDeleted", id);
                            return ok;
                        }
                    }
                    catch { try { tx.Rollback(); } catch { } throw; }
                }
            }
        }

        private static void AddDispatchParams(SqlCommand c, CreateDispatchRequest r)
        {
            c.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)r.DriverId ?? DBNull.Value;
            c.Parameters.Add("@VehicleId", SqlDbType.Int).Value = r.VehicleId;
            c.Parameters.Add("@Title", SqlDbType.NVarChar, 200).Value = (object)r.Title ?? DBNull.Value;
            c.Parameters.Add("@Description", SqlDbType.NVarChar, 1000).Value = (object)r.Description ?? DBNull.Value;
            c.Parameters.Add("@OriginTitle", SqlDbType.NVarChar, 300).Value = (object)r.OriginTitle ?? DBNull.Value;
            c.Parameters.Add("@OriginLatitude", SqlDbType.Decimal).Value = (object)r.OriginLatitude ?? DBNull.Value;
            c.Parameters.Add("@OriginLongitude", SqlDbType.Decimal).Value = (object)r.OriginLongitude ?? DBNull.Value;
            c.Parameters.Add("@DestinationTitle", SqlDbType.NVarChar, 300).Value = (object)r.DestinationTitle ?? DBNull.Value;
            c.Parameters.Add("@DestinationLatitude", SqlDbType.Decimal).Value = (object)r.DestinationLatitude ?? DBNull.Value;
            c.Parameters.Add("@DestinationLongitude", SqlDbType.Decimal).Value = (object)r.DestinationLongitude ?? DBNull.Value;
        }

        private static void AddLocationParams(SqlCommand c, UpdateVehicleLocationRequest r, DateTime at)
        {
            c.Parameters.Add("@VehicleId", SqlDbType.Int).Value = r.VehicleId;
            c.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)r.DriverId ?? DBNull.Value;
            c.Parameters.Add("@MissionId", SqlDbType.Int).Value = (object)r.MissionId ?? DBNull.Value;
            c.Parameters.Add("@Lat", SqlDbType.Decimal).Value = r.Latitude;
            c.Parameters.Add("@Lng", SqlDbType.Decimal).Value = r.Longitude;
            c.Parameters.Add("@Accuracy", SqlDbType.Decimal).Value = (object)r.Accuracy ?? DBNull.Value;
            c.Parameters.Add("@Speed", SqlDbType.Decimal).Value = (object)r.Speed ?? DBNull.Value;
            c.Parameters.Add("@Heading", SqlDbType.Decimal).Value = (object)r.Heading ?? DBNull.Value;
            c.Parameters.Add("@At", SqlDbType.DateTime2).Value = at;
        }

        private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
        {
            const double radius = 6371.0;
            var dLat = (lat2 - lat1) * Math.PI / 180.0;
            var dLon = (lon2 - lon1) * Math.PI / 180.0;
            var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                    Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                    Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return radius * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static int ParseStatus(string status)
        {
            if (int.TryParse(status, out var number)) return number;
            if (string.Equals(status, "Assigned", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(status, "Started", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(status, "Cancelled", StringComparison.OrdinalIgnoreCase)) return 4;
            throw new ArgumentException("وضعیت مأموریت نامعتبر است.");
        }

        private static Dispatch Map(SqlDataReader r)
        {
            return new Dispatch
            {
                Id = Convert.ToInt32(r["Id"]),
                DriverId = r["DriverId"] == DBNull.Value ? (int?)null : Convert.ToInt32(r["DriverId"]),
                VehicleId = Convert.ToInt32(r["VehicleId"]),
                Title = r["Title"] == DBNull.Value ? null : r["Title"].ToString(),
                Description = r["Description"] == DBNull.Value ? null : r["Description"].ToString(),
                OriginTitle = r["OriginTitle"] == DBNull.Value ? null : r["OriginTitle"].ToString(),
                OriginLatitude = r["OriginLatitude"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["OriginLatitude"]),
                OriginLongitude = r["OriginLongitude"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["OriginLongitude"]),
                DestinationTitle = r["DestinationTitle"] == DBNull.Value ? null : r["DestinationTitle"].ToString(),
                DestinationLatitude = r["DestinationLatitude"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["DestinationLatitude"]),
                DestinationLongitude = r["DestinationLongitude"] == DBNull.Value ? (decimal?)null : Convert.ToDecimal(r["DestinationLongitude"]),
                Status = (DispatchStatus)Convert.ToInt32(r["Status"]),
                StartedAtUtc = r["StartedAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["StartedAtUtc"]),
                CompletedAtUtc = r["CompletedAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["CompletedAtUtc"]),
                CreatedAtUtc = Convert.ToDateTime(r["CreatedAtUtc"]),
                UpdatedAtUtc = r["UpdatedAtUtc"] == DBNull.Value ? (DateTime?)null : Convert.ToDateTime(r["UpdatedAtUtc"])
            };
        }
    }
}