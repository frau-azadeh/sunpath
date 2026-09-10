using Microsoft.Extensions.Configuration;
using sunpath.Models;
using sunpath.Services.Interface;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Security.Cryptography;
using System.Text;
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
                SELECT Id, FirstName, LastName, NationalId, Phone, LicenseType, CreatedAt,
                       Username, IsActive
                FROM Drivers ORDER BY Id DESC", connection))
            {
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                    while (await reader.ReadAsync()) drivers.Add(MapDriver(reader));
            }
            return drivers;
        }

        public async Task<Driver> GetByIdAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT Id, FirstName, LastName, NationalId, Phone, LicenseType, CreatedAt,
                       Username, IsActive
                FROM Drivers WHERE Id = @Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                    if (await reader.ReadAsync()) return MapDriver(reader);
            }
            return null;
        }

        public async Task<int> CreateAsync(Driver driver)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                INSERT INTO Drivers
                (FirstName, LastName, NationalId, Phone, LicenseType, Username, PasswordHash, IsActive)
                VALUES
                (@FirstName, @LastName, @NationalId, @Phone, @LicenseType, @Username, @PasswordHash, 1);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", connection))
            {
                AddCommonParameters(command, driver);
                command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = driver.PasswordHash;
                await connection.OpenAsync();
                return Convert.ToInt32(await command.ExecuteScalarAsync());
            }
        }

        public async Task<bool> UpdateAsync(int id, Driver driver)
        {
            var sql = string.IsNullOrWhiteSpace(driver.PasswordHash)
                ? @"UPDATE Drivers SET FirstName=@FirstName, LastName=@LastName,
                    NationalId=@NationalId, Phone=@Phone, LicenseType=@LicenseType,
                    Username=@Username WHERE Id=@Id"
                : @"UPDATE Drivers SET FirstName=@FirstName, LastName=@LastName,
                    NationalId=@NationalId, Phone=@Phone, LicenseType=@LicenseType,
                    Username=@Username, PasswordHash=@PasswordHash WHERE Id=@Id";

            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                AddCommonParameters(command, driver);
                if (!string.IsNullOrWhiteSpace(driver.PasswordHash))
                    command.Parameters.Add("@PasswordHash", SqlDbType.NVarChar, 500).Value = driver.PasswordHash;
                await connection.OpenAsync();
                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand("DELETE FROM Drivers WHERE Id=@Id", connection))
            {
                command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                await connection.OpenAsync();
                return await command.ExecuteNonQueryAsync() > 0;
            }
        }

        public async Task<bool> ExistsByNationalIdAsync(string nationalId, int? excludeId = null)
        {
            return await ExistsAsync("NationalId", nationalId, excludeId);
        }

        public async Task<bool> ExistsByUsernameAsync(string username, int? excludeId = null)
        {
            return await ExistsAsync("Username", username, excludeId);
        }

        private async Task<bool> ExistsAsync(string column, string value, int? excludeId)
        {
            var sql = "SELECT COUNT(1) FROM Drivers WHERE " + column +
                      "=@Value AND (@ExcludeId IS NULL OR Id<>@ExcludeId)";
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(sql, connection))
            {
                command.Parameters.Add("@Value", SqlDbType.NVarChar, 100).Value = value;
                command.Parameters.Add("@ExcludeId", SqlDbType.Int).Value =
                    (object)excludeId ?? DBNull.Value;
                await connection.OpenAsync();
                return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
            }
        }

        public async Task<Driver> FindByCredentialsAsync(string username, string password)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"
                SELECT TOP 1 Id, FirstName, LastName, NationalId, Phone, LicenseType,
                       CreatedAt, Username, PasswordHash, IsActive
                FROM Drivers
                WHERE Username=@Username AND IsActive=1", connection))
            {
                command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = username;
                await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync()) return null;
                    var storedHash = reader["PasswordHash"] == DBNull.Value ? null : reader["PasswordHash"].ToString();
                    if (!PasswordHasher.Verify(password, storedHash)) return null;

                    var driver = MapDriver(reader);
                    driver.PasswordHash = null;
                    return driver;
                }
            }
        }

        private static void AddCommonParameters(SqlCommand command, Driver driver)
        {
            command.Parameters.Add("@FirstName", SqlDbType.NVarChar, 100).Value = driver.FirstName;
            command.Parameters.Add("@LastName", SqlDbType.NVarChar, 100).Value = driver.LastName;
            command.Parameters.Add("@NationalId", SqlDbType.NVarChar, 20).Value = driver.NationalId;
            command.Parameters.Add("@Phone", SqlDbType.NVarChar, 20).Value = driver.Phone;
            command.Parameters.Add("@LicenseType", SqlDbType.Int).Value = driver.LicenseType;
            command.Parameters.Add("@Username", SqlDbType.NVarChar, 100).Value = driver.Username;
        }

        private static Driver MapDriver(SqlDataReader reader)
        {
            return new Driver
            {
                Id = Convert.ToInt32(reader["Id"]),
                FirstName = reader["FirstName"] as string,
                LastName = reader["LastName"] as string,
                NationalId = reader["NationalId"] as string,
                Phone = reader["Phone"] as string,
                LicenseType = reader["LicenseType"] == DBNull.Value ? 0 : Convert.ToInt32(reader["LicenseType"]),
                CreatedAt = reader["CreatedAt"] == DBNull.Value ? DateTime.MinValue : Convert.ToDateTime(reader["CreatedAt"]),
                Username = reader["Username"] == DBNull.Value ? null : reader["Username"].ToString(),
                IsActive = reader["IsActive"] == DBNull.Value || Convert.ToBoolean(reader["IsActive"])
            };
        }
    }

    internal static class PasswordHasher
    {
        // PBKDF2 بدون نیاز به پکیج خارجی؛ مناسب پروژه ASP.NET Core 2.1.
        public static string Hash(string password)
        {
            var salt = new byte[16];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, 100000))
            {
                var hash = pbkdf2.GetBytes(32);
                return "PBKDF2$100000$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
            }
        }

        public static bool Verify(string password, string encoded)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(encoded)) return false;
                var parts = encoded.Split('$');
                if (parts.Length != 4 || parts[0] != "PBKDF2") return false;
                var iterations = int.Parse(parts[1]);
                var salt = Convert.FromBase64String(parts[2]);
                var expected = Convert.FromBase64String(parts[3]);
                using (var pbkdf2 = new Rfc2898DeriveBytes(password, salt, iterations))
                {
                    var actual = pbkdf2.GetBytes(expected.Length);
                    return FixedTimeEquals(actual, expected);
                }
            }
            catch { return false; }
        }

        private static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            var diff = 0;
            for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}