using Microsoft.Extensions.Configuration;
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
        private readonly string _connectionString;

        public VehicleService(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SunPathConnection");
        }

        public async Task<List<Vehicle>> GetAllVehiclesAsync()
        {
            var vehicles = new List<Vehicle>();

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT 
                    v.Id,
                    v.PlateNumber,
                    v.Model,
                    v.Status,
                    v.LastLatitude,
                    v.LastLongitude,
                    v.LastUpdateAt,
                    v.Speed,
                    v.Heading,
                    v.Latitude,
                    v.Longitude,
                    v.LastUpdate,
                    v.VehicleType,
                    v.InsuranceNumber,
                    v.InsuranceExpiryDate,
                    v.CurrentDriverId,
                    v.FuelConsumedLiters,
                    v.TripDistanceKm,
                    v.TripDurationSeconds,
                    v.StopDurationSeconds,
                    d.FirstName + ' ' + d.LastName AS CurrentDriverName,
                    m.Id AS ActiveDispatchId,
                    m.DriverId AS ActiveDispatchDriverId,
                    m.OriginTitle AS OriginAddress,
                    m.OriginLatitude AS OriginLat,
                    m.OriginLongitude AS OriginLng,
                    m.DestinationTitle AS DestinationAddress,
                    m.DestinationLatitude AS DestinationLat,
                    m.DestinationLongitude AS DestinationLng,
                    CASE m.Status WHEN 1 THEN 'Assigned' WHEN 2 THEN 'InProgress' END AS DispatchStatus
                FROM Vehicles v
                LEFT JOIN Drivers d ON d.Id = v.CurrentDriverId
                OUTER APPLY (
                    SELECT TOP 1 *
                    FROM Missions mx
                    WHERE mx.VehicleId = v.Id AND mx.Status IN (1,2)
                    ORDER BY mx.Id DESC
                ) m
                ORDER BY v.Id DESC", connection))
            {
                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        vehicles.Add(MapVehicle(reader));
                    }
                }
            }

            return vehicles;
        }

        public async Task<Vehicle> GetByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT 
                    v.Id,
                    v.PlateNumber,
                    v.Model,
                    v.Status,
                    v.LastLatitude,
                    v.LastLongitude,
                    v.LastUpdateAt,
                    v.Speed,
                    v.Heading,
                    v.Latitude,
                    v.Longitude,
                    v.LastUpdate,
                    v.VehicleType,
                    v.InsuranceNumber,
                    v.InsuranceExpiryDate,
                    v.CurrentDriverId,
                    v.FuelConsumedLiters,
                    v.TripDistanceKm,
                    v.TripDurationSeconds,
                    v.StopDurationSeconds,
                    d.FirstName + ' ' + d.LastName AS CurrentDriverName,
                    m.Id AS ActiveDispatchId,
                    m.DriverId AS ActiveDispatchDriverId,
                    m.OriginTitle AS OriginAddress,
                    m.OriginLatitude AS OriginLat,
                    m.OriginLongitude AS OriginLng,
                    m.DestinationTitle AS DestinationAddress,
                    m.DestinationLatitude AS DestinationLat,
                    m.DestinationLongitude AS DestinationLng,
                    CASE m.Status WHEN 1 THEN 'Assigned' WHEN 2 THEN 'InProgress' END AS DispatchStatus
                FROM Vehicles v
                LEFT JOIN Drivers d ON d.Id = v.CurrentDriverId
                OUTER APPLY (
                    SELECT TOP 1 *
                    FROM Missions mx
                    WHERE mx.VehicleId = v.Id AND mx.Status IN (1,2)
                    ORDER BY mx.Id DESC
                ) m
                WHERE v.Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return MapVehicle(reader);
                    }
                }
            }

            return null;
        }

        public async Task<int> CreateAsync(Vehicle vehicle)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                INSERT INTO Vehicles
                (
                    PlateNumber,
                    Model,
                    Status,
                    Speed,
                    Heading,
                    VehicleType,
                    InsuranceNumber,
                    InsuranceExpiryDate,
                    CurrentDriverId
                )
                VALUES
                (
                    @PlateNumber,
                    @Model,
                    @Status,
                    @Speed,
                    @Heading,
                    @VehicleType,
                    @InsuranceNumber,
                    @InsuranceExpiryDate,
                    @CurrentDriverId
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
            {
                command.Parameters.Add("@PlateNumber", SqlDbType.NVarChar, 20).Value = vehicle.PlateNumber;
                command.Parameters.Add("@Model", SqlDbType.NVarChar, 50).Value = (object)vehicle.Model ?? DBNull.Value;
                command.Parameters.Add("@Status", SqlDbType.Int).Value = vehicle.Status;
                command.Parameters.Add("@Speed", SqlDbType.Float).Value = vehicle.Speed;
                command.Parameters.Add("@Heading", SqlDbType.Float).Value = vehicle.Heading;
                command.Parameters.Add("@VehicleType", SqlDbType.Int).Value = vehicle.VehicleType;
                command.Parameters.Add("@InsuranceNumber", SqlDbType.NVarChar, 50).Value = (object)vehicle.InsuranceNumber ?? DBNull.Value;
                command.Parameters.Add("@InsuranceExpiryDate", SqlDbType.DateTime).Value = (object)vehicle.InsuranceExpiryDate ?? DBNull.Value;
                command.Parameters.Add("@CurrentDriverId", SqlDbType.Int).Value = (object)vehicle.CurrentDriverId ?? DBNull.Value;

                await connection.OpenAsync();

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        public async Task<bool> UpdateAsync(int id, Vehicle vehicle)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                UPDATE Vehicles
                SET
                    PlateNumber = @PlateNumber,
                    Model = @Model,
                    Status = @Status,
                    VehicleType = @VehicleType,
                    InsuranceNumber = @InsuranceNumber,
                    InsuranceExpiryDate = @InsuranceExpiryDate,
                    CurrentDriverId = @CurrentDriverId
                WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@PlateNumber", SqlDbType.NVarChar, 20).Value = vehicle.PlateNumber;
                command.Parameters.Add("@Model", SqlDbType.NVarChar, 50).Value = (object)vehicle.Model ?? DBNull.Value;
                command.Parameters.Add("@Status", SqlDbType.Int).Value = vehicle.Status;
                command.Parameters.Add("@VehicleType", SqlDbType.Int).Value = vehicle.VehicleType;
                command.Parameters.Add("@InsuranceNumber", SqlDbType.NVarChar, 50).Value = (object)vehicle.InsuranceNumber ?? DBNull.Value;
                command.Parameters.Add("@InsuranceExpiryDate", SqlDbType.DateTime).Value = (object)vehicle.InsuranceExpiryDate ?? DBNull.Value;
                command.Parameters.Add("@CurrentDriverId", SqlDbType.Int).Value = (object)vehicle.CurrentDriverId ?? DBNull.Value;

                await connection.OpenAsync();
                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                DELETE FROM Vehicles
                WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                await connection.OpenAsync();
                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> UpdateVehicleStatusAsync(
            int id,
            double? latitude,
            double? longitude,
            double speed,
            double heading)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
        UPDATE Vehicles
        SET
            Latitude = @Latitude,
            Longitude = @Longitude,
            LastLatitude = @LastLatitude,
            LastLongitude = @LastLongitude,
            Speed = @Speed,
            Heading = @Heading,
            LastUpdate = GETDATE(),
            LastUpdateAt = GETDATE()
        WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                command.Parameters.Add("@Latitude", SqlDbType.Float).Value =
                    latitude.HasValue ? (object)latitude.Value : DBNull.Value;

                command.Parameters.Add("@Longitude", SqlDbType.Float).Value =
                    longitude.HasValue ? (object)longitude.Value : DBNull.Value;

                var lastLatitudeParameter = command.Parameters.Add(
                    "@LastLatitude",
                    SqlDbType.Decimal);

                lastLatitudeParameter.Precision = 9;
                lastLatitudeParameter.Scale = 6;
                lastLatitudeParameter.Value = latitude.HasValue
                    ? (object)Convert.ToDecimal(latitude.Value)
                    : DBNull.Value;

                var lastLongitudeParameter = command.Parameters.Add(
                    "@LastLongitude",
                    SqlDbType.Decimal);

                lastLongitudeParameter.Precision = 9;
                lastLongitudeParameter.Scale = 6;
                lastLongitudeParameter.Value = longitude.HasValue
                    ? (object)Convert.ToDecimal(longitude.Value)
                    : DBNull.Value;

                command.Parameters.Add("@Speed", SqlDbType.Float).Value = speed;
                command.Parameters.Add("@Heading", SqlDbType.Float).Value = heading;

                await connection.OpenAsync();

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> ExistsByPlateNumberAsync(string plateNumber, int? excludeId = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT COUNT(1)
                FROM Vehicles
                WHERE PlateNumber = @PlateNumber
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)", connection))
            {
                command.Parameters.Add("@PlateNumber", SqlDbType.NVarChar, 20).Value = plateNumber;
                command.Parameters.Add("@ExcludeId", SqlDbType.Int).Value = (object)excludeId ?? DBNull.Value;

                await connection.OpenAsync();

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
        }

        private Vehicle MapVehicle(SqlDataReader reader)
        {
            return new Vehicle
            {
                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                PlateNumber = reader["PlateNumber"] != DBNull.Value ? reader["PlateNumber"].ToString() : null,
                Model = reader["Model"] != DBNull.Value ? reader["Model"].ToString() : null,
                Status = reader["Status"] != DBNull.Value ? Convert.ToInt32(reader["Status"]) : 0,
                LastLatitude = reader["LastLatitude"] != DBNull.Value ? Convert.ToDecimal(reader["LastLatitude"]) : (decimal?)null,
                LastLongitude = reader["LastLongitude"] != DBNull.Value ? Convert.ToDecimal(reader["LastLongitude"]) : (decimal?)null,
                LastUpdateAt = reader["LastUpdateAt"] != DBNull.Value ? Convert.ToDateTime(reader["LastUpdateAt"]) : (DateTime?)null,
                Speed = reader["Speed"] != DBNull.Value ? Convert.ToDouble(reader["Speed"]) : 0,
                Heading = reader["Heading"] != DBNull.Value ? Convert.ToDouble(reader["Heading"]) : 0,
                Latitude = reader["Latitude"] != DBNull.Value ? Convert.ToDouble(reader["Latitude"]) : (double?)null,
                Longitude = reader["Longitude"] != DBNull.Value ? Convert.ToDouble(reader["Longitude"]) : (double?)null,
                LastUpdate = reader["LastUpdate"] != DBNull.Value ? Convert.ToDateTime(reader["LastUpdate"]) : (DateTime?)null,
                VehicleType = reader["VehicleType"] != DBNull.Value ? Convert.ToInt32(reader["VehicleType"]) : 0,
                InsuranceNumber = reader["InsuranceNumber"] != DBNull.Value ? reader["InsuranceNumber"].ToString() : null,
                InsuranceExpiryDate = reader["InsuranceExpiryDate"] != DBNull.Value ? Convert.ToDateTime(reader["InsuranceExpiryDate"]) : (DateTime?)null,
                CurrentDriverId = reader["CurrentDriverId"] != DBNull.Value ? Convert.ToInt32(reader["CurrentDriverId"]) : (int?)null,
                FuelConsumedLiters = reader["FuelConsumedLiters"] != DBNull.Value ? Convert.ToDouble(reader["FuelConsumedLiters"]) : 0,
                TripDistanceKm = reader["TripDistanceKm"] != DBNull.Value ? Convert.ToDouble(reader["TripDistanceKm"]) : 0,
                TripDurationSeconds = reader["TripDurationSeconds"] != DBNull.Value ? Convert.ToInt32(reader["TripDurationSeconds"]) : 0,
                StopDurationSeconds = reader["StopDurationSeconds"] != DBNull.Value ? Convert.ToInt32(reader["StopDurationSeconds"]) : 0,
                CurrentDriverName = reader["CurrentDriverName"] != DBNull.Value ? reader["CurrentDriverName"].ToString() : null,
                ActiveDispatchId = reader["ActiveDispatchId"] != DBNull.Value ? Convert.ToInt32(reader["ActiveDispatchId"]) : (int?)null,
                ActiveDispatchDriverId = reader["ActiveDispatchDriverId"] != DBNull.Value ? Convert.ToInt32(reader["ActiveDispatchDriverId"]) : (int?)null,
                OriginAddress = reader["OriginAddress"] != DBNull.Value ? reader["OriginAddress"].ToString() : null,
                OriginLat = reader["OriginLat"] != DBNull.Value ? Convert.ToDecimal(reader["OriginLat"]) : (decimal?)null,
                OriginLng = reader["OriginLng"] != DBNull.Value ? Convert.ToDecimal(reader["OriginLng"]) : (decimal?)null,
                DestinationAddress = reader["DestinationAddress"] != DBNull.Value ? reader["DestinationAddress"].ToString() : null,
                DestinationLat = reader["DestinationLat"] != DBNull.Value ? Convert.ToDecimal(reader["DestinationLat"]) : (decimal?)null,
                DestinationLng = reader["DestinationLng"] != DBNull.Value ? Convert.ToDecimal(reader["DestinationLng"]) : (decimal?)null,
                DispatchStatus = reader["DispatchStatus"] != DBNull.Value ? reader["DispatchStatus"].ToString() : null
            };
        }
    }
}
