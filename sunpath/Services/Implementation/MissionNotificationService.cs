using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.SignalR;
using sunpath.Hubs;
using sunpath.Models;
namespace sunpath.Services.Implementation
{
    public class MissionNotificationService
    {
        private readonly string _connectionString;
        private readonly IHubContext<VehicleHub> _hub;
        private readonly ILogger<MissionNotificationService> _logger;
        public MissionNotificationService(IConfiguration configuration, IHubContext<VehicleHub> hub, ILogger<MissionNotificationService> logger)
        { _connectionString = configuration.GetConnectionString("SunPathConnection"); _hub = hub; _logger = logger; }
        public static string Group(string audience, int? driverId) => audience == "driver" ? "notifications-driver-" + driverId : "notifications-admin";
        public static async Task<MissionNotification> InsertAsync(SqlConnection connection, SqlTransaction transaction, int missionId, string audience, int? driverId, string kind, string title, string message)
        {
            var item = new MissionNotification { MissionId = missionId, Audience = audience, DriverId = driverId, Kind = kind, Title = title, Message = message, CreatedAtUtc = DateTime.UtcNow };
            using (var command = new SqlCommand(@"INSERT INTO MissionNotifications (Audience,DriverId,MissionId,Kind,Title,Message,CreatedAtUtc)
VALUES (@Audience,@DriverId,@MissionId,@Kind,@Title,@Message,@Now); SELECT CAST(SCOPE_IDENTITY() AS INT);", connection, transaction))
            {
                command.Parameters.Add("@Audience", SqlDbType.NVarChar, 10).Value = audience;
                command.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)driverId ?? DBNull.Value;
                command.Parameters.Add("@MissionId", SqlDbType.Int).Value = missionId;
                command.Parameters.Add("@Kind", SqlDbType.NVarChar, 20).Value = kind;
                command.Parameters.Add("@Title", SqlDbType.NVarChar, 300).Value = title;
                command.Parameters.Add("@Message", SqlDbType.NVarChar, 1000).Value = message;
                command.Parameters.Add("@Now", SqlDbType.DateTime2).Value = item.CreatedAtUtc;
                item.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
            }
            return item;
        }
        public async Task PublishAsync(MissionNotification item)
        {
            if (item == null) return;
            // Durable commit comes first. Delivery failure must not roll back a successful action.
            try { await _hub.Clients.Group(Group(item.Audience, item.DriverId)).SendAsync("NotificationCreated", item); }
            catch (Exception error) { _logger.LogWarning(error, "Notification {NotificationId} will be recovered by inbox polling", item.Id); }
        }
        public async Task<object> ListAsync(string audience, int? driverId)
        {
            var items = new List<MissionNotification>(); var unread = 0;
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"SELECT TOP 100 * FROM MissionNotifications
WHERE Audience=@Audience AND (DriverId=@DriverId OR (@DriverId IS NULL AND DriverId IS NULL)) ORDER BY CASE WHEN ReadAtUtc IS NULL THEN 0 ELSE 1 END, Id DESC;
SELECT COUNT(*) FROM MissionNotifications WHERE Audience=@Audience AND (DriverId=@DriverId OR (@DriverId IS NULL AND DriverId IS NULL)) AND ReadAtUtc IS NULL;", connection))
            {
                Scope(command, audience, driverId); await connection.OpenAsync();
                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync()) items.Add(new MissionNotification {
                        Id = Convert.ToInt32(reader["Id"]), MissionId = Convert.ToInt32(reader["MissionId"]), Audience = reader["Audience"].ToString(),
                        DriverId = reader["DriverId"] == DBNull.Value ? (int?)null : Convert.ToInt32(reader["DriverId"]), Kind = reader["Kind"].ToString(), Title = reader["Title"].ToString(), Message = reader["Message"].ToString(),
                        CreatedAtUtc = DateTime.SpecifyKind(Convert.ToDateTime(reader["CreatedAtUtc"]), DateTimeKind.Utc), ReadAtUtc = reader["ReadAtUtc"] == DBNull.Value ? (DateTime?)null : DateTime.SpecifyKind(Convert.ToDateTime(reader["ReadAtUtc"]), DateTimeKind.Utc)
                    });
                    if (await reader.NextResultAsync() && await reader.ReadAsync()) unread = Convert.ToInt32(reader[0]);
                }
            }
            return new { items, unreadCount = unread };
        }
        public async Task<bool> MarkReadAsync(int id, string audience, int? driverId)
        {
            using (var connection = new SqlConnection(_connectionString))
            using (var command = new SqlCommand(@"UPDATE MissionNotifications SET ReadAtUtc=COALESCE(ReadAtUtc,SYSUTCDATETIME())
WHERE Id=@Id AND Audience=@Audience AND (DriverId=@DriverId OR (@DriverId IS NULL AND DriverId IS NULL));", connection))
            {
                Scope(command, audience, driverId); command.Parameters.Add("@Id", SqlDbType.Int).Value = id;
                await connection.OpenAsync(); return await command.ExecuteNonQueryAsync() > 0;
            }
        }
        private static void Scope(SqlCommand command, string audience, int? driverId)
        { command.Parameters.Add("@Audience", SqlDbType.NVarChar, 10).Value = audience; command.Parameters.Add("@DriverId", SqlDbType.Int).Value = (object)driverId ?? DBNull.Value; }
    }
}
