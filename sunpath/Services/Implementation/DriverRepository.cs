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
    public class DriverRepository : IDriverRepository
    {
        private readonly string _connectionString;

        public DriverRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("SunPathConnection");
        }

        public async Task<List<Driver>> GetAllAsync()
        {
            var drivers = new List<Driver>();

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT Id, FirstName, LastName, NationalId, Phone, LicenseType, CreatedAt
                FROM Drivers
                ORDER BY Id DESC", connection))
            {
                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        drivers.Add(MapDriver(reader));
                    }
                }
            }

            return drivers;
        }

        public async Task<Driver> GetByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT Id, FirstName, LastName, NationalId, Phone, LicenseType, CreatedAt
                FROM Drivers
                WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        return MapDriver(reader);
                    }
                }
            }

            return null;
        }

        public async Task<int> CreateAsync(Driver driver)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                INSERT INTO Drivers
                (
                    FirstName,
                    LastName,
                    NationalId,
                    Phone,
                    LicenseType
                )
                VALUES
                (
                    @FirstName,
                    @LastName,
                    @NationalId,
                    @Phone,
                    @LicenseType
                );

                SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
            {
                command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = driver.FirstName;
                command.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = driver.LastName;
                command.Parameters.Add("@NationalId", SqlDbType.NVarChar, 20).Value = driver.NationalId;
                command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = driver.Phone;
                command.Parameters.Add("@LicenseType", SqlDbType.Int).Value = driver.LicenseType;

                await connection.OpenAsync();

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result);
            }
        }

        public async Task<bool> UpdateAsync(int id, Driver driver)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                UPDATE Drivers
                SET
                    FirstName = @FirstName,
                    LastName = @LastName,
                    NationalId = @NationalId,
                    Phone = @Phone,
                    LicenseType = @LicenseType
                WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = driver.FirstName;
                command.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = driver.LastName;
                command.Parameters.Add("@NationalId", SqlDbType.NVarChar, 20).Value = driver.NationalId;
                command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = driver.Phone;
                command.Parameters.Add("@LicenseType", SqlDbType.Int).Value = driver.LicenseType;

                await connection.OpenAsync();

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                DELETE FROM Drivers
                WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                await connection.OpenAsync();

                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> ExistsByNationalIdAsync(string nationalId, int? excludeId = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT COUNT(1)
                FROM Drivers
                WHERE NationalId = @NationalId
                  AND (@ExcludeId IS NULL OR Id <> @ExcludeId)", connection))
            {
                command.Parameters.Add("@NationalId", SqlDbType.NVarChar, 20).Value = nationalId;
                command.Parameters.Add("@ExcludeId", SqlDbType.Int).Value = (object)excludeId ?? DBNull.Value;

                await connection.OpenAsync();

                var result = await command.ExecuteScalarAsync();
                return Convert.ToInt32(result) > 0;
            }
        }

        private Driver MapDriver(SqlDataReader reader)
        {
            return new Driver
            {
                Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
                FirstName = reader["FirstName"] != DBNull.Value ? reader["FirstName"].ToString() : null,
                LastName = reader["LastName"] != DBNull.Value ? reader["LastName"].ToString() : null,
                NationalId = reader["NationalId"] != DBNull.Value ? reader["NationalId"].ToString() : null,
                Phone = reader["Phone"] != DBNull.Value ? reader["Phone"].ToString() : null,
                LicenseType = reader["LicenseType"] != DBNull.Value ? Convert.ToInt32(reader["LicenseType"]) : 0,
                CreatedAt = reader["CreatedAt"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedAt"]) : DateTime.MinValue
            };
        }
    }
}
