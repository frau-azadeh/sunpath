using Microsoft.Extensions.Configuration;
using sunpath.Models;
using sunpath.Services.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
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

        public List<Driver> GetAll()
        {
            var drivers = new List<Driver>();

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT Id, FirstName, LastName, NationalId, Phone, LicenseType, CreatedAt
                FROM Drivers
                ORDER BY Id DESC", connection))
            {
                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        drivers.Add(MapDriver(reader));
                    }
                }
            }

            return drivers;
        }

        public Driver GetById(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT Id, FirstName, LastName, NationalId, Phone, LicenseType, CreatedAt
                FROM Drivers
                WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                connection.Open();

                using (var reader = command.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        return MapDriver(reader);
                    }
                }
            }

            return null;
        }

        public int Create(Driver driver)
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
                command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = (object)driver.FirstName ?? DBNull.Value;
                command.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = (object)driver.LastName ?? DBNull.Value;
                command.Parameters.Add("@NationalId", SqlDbType.NVarChar, 20).Value = (object)driver.NationalId ?? DBNull.Value;
                command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object)driver.Phone ?? DBNull.Value;
                command.Parameters.Add("@LicenseType", SqlDbType.Int).Value = driver.LicenseType;

                connection.Open();

                return (int)command.ExecuteScalar();
            }
        }

        public bool Update(int id, Driver driver)
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
                command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = (object)driver.FirstName ?? DBNull.Value;
                command.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = (object)driver.LastName ?? DBNull.Value;
                command.Parameters.Add("@NationalId", SqlDbType.NVarChar, 20).Value = (object)driver.NationalId ?? DBNull.Value;
                command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = (object)driver.Phone ?? DBNull.Value;
                command.Parameters.Add("@LicenseType", SqlDbType.Int).Value = driver.LicenseType;

                connection.Open();

                return command.ExecuteNonQuery() > 0;
            }
        }

        public bool Delete(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                DELETE FROM Drivers
                WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;

                connection.Open();

                return command.ExecuteNonQuery() > 0;
            }
        }

        public bool ExistsByNationalId(string nationalId, int? excludeId = null)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT COUNT(1)
                FROM Drivers
                WHERE NationalId = @NationalId
                AND (@ExcludeId IS NULL OR Id <> @ExcludeId)", connection))
            {
                command.Parameters.Add("@NationalId", SqlDbType.NVarChar, 20).Value = (object)nationalId ?? DBNull.Value;
                command.Parameters.Add("@ExcludeId", SqlDbType.Int).Value = (object)excludeId ?? DBNull.Value;

                connection.Open();

                return (int)command.ExecuteScalar() > 0;
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