using sunpath.Data;
using sunpath.Models;
using sunpath.Services.Interface;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Services.Implementation
{
    public class VehicleService : IVehicleService
    {
        private readonly DbHelper _db;

        public VehicleService(DbHelper db)
        {
            _db = db;
        }

        public async Task<IEnumerable<Vehicle>> GetAllVehiclesAsync()
        {
            var list = new List<Vehicle>();
            using (var conn = _db.GetConnection())
            {
                // ۱. باز کردن اتصال به صورت Async
                await conn.OpenAsync();

                // ۲. نوشتن کوئری SQL
                var sql = "SELECT Id, PlateNumber, Status, Latitude, Longitude, Speed, Heading, LastUpdate FROM Vehicles";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    using (var reader = await cmd.ExecuteReaderAsync())
                    {
                        // ۳. خواندن سطر به سطر نتایج
                        while (await reader.ReadAsync())
                        {
                            list.Add(new Vehicle
                            {
                                Id = (int)reader["Id"],
                                PlateNumber = reader["PlateNumber"].ToString(),
                                Status = reader["Status"].ToString(),
                                Latitude = (double)reader["Latitude"],
                                Longitude = (double)reader["Longitude"],
                                Speed = (double)reader["Speed"],
                                Heading = (double)reader["Heading"],
                                LastUpdate = (DateTime)reader["LastUpdate"]
                            });
                        }
                    }
                }
            }
            return list;
        }

        public async Task UpdateVehicleStatusAsync(int id, double lat, double lng, double speed, double heading)
        {
            using (var conn = _db.GetConnection())
            {
                await conn.OpenAsync();
                var sql = @"UPDATE Vehicles 
                            SET Latitude = @lat, Longitude = @lng, Speed = @speed, Heading = @heading, LastUpdate = GETDATE() 
                            WHERE Id = @id";

                using (var cmd = new SqlCommand(sql, conn))
                {
                    // ۴. استفاده از پارامترها برای جلوگیری از SQL Injection
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@lat", lat);
                    cmd.Parameters.AddWithValue("@lng", lng);
                    cmd.Parameters.AddWithValue("@speed", speed);
                    cmd.Parameters.AddWithValue("@heading", heading);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
        }
    }
}