using Microsoft.AspNetCore.SignalR;
using sunpath.Data;
using sunpath.Hubs;
using sunpath.Models;
using sunpath.Services.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace sunpath.Services.Implementation
{
    public class VehicleService : IVehicleService
    {
        private readonly DbHelper _db;
        private readonly IHubContext<VehicleHub> _hubContext;

        public VehicleService(
            DbHelper db,
            IHubContext<VehicleHub> hubContext)
        {
            _db = db;
            _hubContext = hubContext;
        }

        public async Task<IEnumerable<Vehicle>> GetAllVehiclesAsync()
        {
            var list = new List<Vehicle>();

            using (var conn = _db.GetConnection())
            {
                await conn.OpenAsync();

                var sql = @"
                    SELECT
                        Id,
                        PlateNumber,
                        Status,
                        Latitude,
                        Longitude,
                        Speed,
                        Heading,
                        LastUpdate
                    FROM Vehicles";

                using (var cmd = new SqlCommand(sql, conn))
                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        list.Add(new Vehicle
                        {
                            Id = reader["Id"] == DBNull.Value
                                ? 0
                                : Convert.ToInt32(reader["Id"]),

                            PlateNumber = reader["PlateNumber"] == DBNull.Value
                                ? string.Empty
                                : reader["PlateNumber"].ToString(),

                            Status = reader["Status"] == DBNull.Value
                                ? string.Empty
                                : reader["Status"].ToString(),

                            Latitude = reader["Latitude"] == DBNull.Value
                                ? 0
                                : Convert.ToDouble(reader["Latitude"]),

                            Longitude = reader["Longitude"] == DBNull.Value
                                ? 0
                                : Convert.ToDouble(reader["Longitude"]),

                            Speed = reader["Speed"] == DBNull.Value
                                ? 0
                                : Convert.ToDouble(reader["Speed"]),

                            Heading = reader["Heading"] == DBNull.Value
                                ? 0
                                : Convert.ToDouble(reader["Heading"]),

                            LastUpdate = reader["LastUpdate"] == DBNull.Value
                                ? DateTime.MinValue
                                : Convert.ToDateTime(reader["LastUpdate"])
                        });
                    }
                }
            }

            return list;
        }

        public async Task UpdateVehicleStatusAsync(
            int id,
            double lat,
            double lng,
            double speed,
            double heading)
        {
            DateTime lastUpdate = DateTime.Now;

            using (var conn = _db.GetConnection())
            {
                await conn.OpenAsync();

                var sql = @"
                    UPDATE Vehicles
                    SET
                        Latitude = @lat,
                        Longitude = @lng,
                        Speed = @speed,
                        Heading = @heading,
                        LastUpdate = GETDATE()
                    WHERE Id = @id";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    cmd.Parameters.Add("@id", SqlDbType.Int).Value = id;

                    var latitudeParameter = cmd.Parameters.Add("@lat", SqlDbType.Float);
                    latitudeParameter.Value = lat;

                    var longitudeParameter = cmd.Parameters.Add("@lng", SqlDbType.Float);
                    longitudeParameter.Value = lng;

                    var speedParameter = cmd.Parameters.Add("@speed", SqlDbType.Float);
                    speedParameter.Value = speed;

                    var headingParameter = cmd.Parameters.Add("@heading", SqlDbType.Float);
                    headingParameter.Value = heading;

                    var affectedRows = await cmd.ExecuteNonQueryAsync();

                    // اگر خودرویی با این ID پیدا نشد، پیام SignalR ارسال نشود
                    if (affectedRows == 0)
                    {
                        return;
                    }
                }
            }

            // ارسال موقعیت جدید به تمام کلاینت‌های متصل به SignalR
            var payload = new
            {
                id = id,
                latitude = lat,
                longitude = lng,
                speed = speed,
                heading = heading,
                lastUpdate = lastUpdate
            };

            await _hubContext.Clients.All.SendAsync(
                "VehiclePositionChanged",
                payload
            );
        }
    }
}
