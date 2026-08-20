using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;

namespace sunpath.Data
{
    public class DbHelper
    {
        private readonly string _connectionString;

        public DbHelper(IConfiguration configuration)
        {
            _connectionString =
                configuration.GetConnectionString("SunPathConnection");
        }

        public SqlConnection GetConnection()
        {
            return new SqlConnection(_connectionString);
        }

        public async Task<object> ExecuteScalarAsync(
            string query,
            params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.Text;

                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                await connection.OpenAsync();

                return await command.ExecuteScalarAsync();
            }
        }

        public async Task<int> ExecuteNonQueryAsync(
            string query,
            params SqlParameter[] parameters)
        {
            using (var connection = GetConnection())
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.Text;

                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                await connection.OpenAsync();

                return await command.ExecuteNonQueryAsync();
            }
        }

        public async Task<List<Dictionary<string, object>>> ExecuteQueryAsync(
            string query,
            params SqlParameter[] parameters)
        {
            var result = new List<Dictionary<string, object>>();

            using (var connection = GetConnection())
            using (var command = new SqlCommand(query, connection))
            {
                command.CommandType = CommandType.Text;

                if (parameters != null && parameters.Length > 0)
                {
                    command.Parameters.AddRange(parameters);
                }

                await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var row = new Dictionary<string, object>();

                        for (var i = 0; i < reader.FieldCount; i++)
                        {
                            row[reader.GetName(i)] =
                                reader.IsDBNull(i)
                                    ? null
                                    : reader.GetValue(i);
                        }

                        result.Add(row);
                    }
                }
            }

            return result;
        }
    }
}
