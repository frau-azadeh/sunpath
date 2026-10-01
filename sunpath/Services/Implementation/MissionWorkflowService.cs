using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using sunpath.Models;
using Microsoft.AspNetCore.SignalR;
using sunpath.Hubs;
using sunpath.Services.Interface;
namespace sunpath.Services.Implementation
{
    public class MissionWorkflowService
    {
        private readonly IHubContext<VehicleHub> _hub;
        private readonly string _connectionString;
        private readonly IDispatchService _dispatches;
        private readonly MissionNotificationService _notifications;
        public MissionWorkflowService(IConfiguration configuration, IDispatchService dispatches, MissionNotificationService notifications, IHubContext<VehicleHub> hub)
        { _connectionString = configuration.GetConnectionString("SunPathConnection"); _dispatches = dispatches; _notifications = notifications; _hub = hub; }
        public async Task<Dispatch> StageAsync(int missionId, int driverId, bool complete)
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    int status; bool accepted, arrived; int vehicleId;
                    using (var command = new SqlCommand("SELECT Status,AcceptedAtUtc,ArrivedAtUtc,VehicleId FROM Missions WITH (UPDLOCK,HOLDLOCK) WHERE Id=@Id AND DriverId=@DriverId;", connection, transaction))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = missionId; command.Parameters.Add("@DriverId", SqlDbType.Int).Value = driverId;
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync()) throw new InvalidOperationException("این مأموریت به شما اختصاص داده نشده است.");
                            status = Convert.ToInt32(reader["Status"]); accepted = reader["AcceptedAtUtc"] != DBNull.Value; arrived = reader["ArrivedAtUtc"] != DBNull.Value; vehicleId = Convert.ToInt32(reader["VehicleId"]);
                        }
                    }
                    if (status != (complete ? 3 : 2))
                    {
                        if (complete ? status != 2 || !arrived : status != 1 || !accepted)
                            throw new InvalidOperationException(complete ? "ابتدا رسیدن به مقصد را ثبت کنید." : "ابتدا مأموریت را قبول کنید.");
                        using (var command = new SqlCommand(complete
                            ? "UPDATE Missions SET Status=3,CompletedAtUtc=SYSUTCDATETIME(),UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@Id; UPDATE Vehicles SET CurrentDriverId=NULL WHERE Id=@VehicleId AND CurrentDriverId=@DriverId;"
                            : "UPDATE Missions SET Status=2,StartedAtUtc=COALESCE(StartedAtUtc,SYSUTCDATETIME()),UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@Id;", connection, transaction))
                        {
                            command.Parameters.Add("@Id", SqlDbType.Int).Value = missionId; command.Parameters.Add("@DriverId", SqlDbType.Int).Value = driverId; command.Parameters.Add("@VehicleId", SqlDbType.Int).Value = vehicleId;
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                    transaction.Commit();
                }
            }
            var dispatch = await _dispatches.GetByIdAsync(missionId);
            try { await _hub.Clients.All.SendAsync("DispatchStatusChanged", dispatch); } catch { /* The committed state is recoverable via refresh/polling. */ }
            return dispatch;
        }

        public async Task<Dispatch> RecordAsync(int missionId, int driverId, bool arrived)
        {
            MissionNotification notification = null;
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction = connection.BeginTransaction())
                {
                    int status; bool accepted, alreadyArrived; string title, driverName;
                    using (var command = new SqlCommand(@"SELECT m.Status,m.AcceptedAtUtc,m.ArrivedAtUtc,m.Title,
COALESCE(d.FirstName,'')+' '+COALESCE(d.LastName,'') AS DriverName
FROM Missions m WITH (UPDLOCK,HOLDLOCK) LEFT JOIN Drivers d ON d.Id=m.DriverId
WHERE m.Id=@Id AND m.DriverId=@DriverId;", connection, transaction))
                    {
                        command.Parameters.Add("@Id", SqlDbType.Int).Value = missionId; command.Parameters.Add("@DriverId", SqlDbType.Int).Value = driverId;
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync()) throw new InvalidOperationException("این مأموریت به حساب شما اختصاص داده نشده است.");
                            status = Convert.ToInt32(reader["Status"]); accepted = reader["AcceptedAtUtc"] != DBNull.Value; alreadyArrived = reader["ArrivedAtUtc"] != DBNull.Value;
                            title = reader["Title"] == DBNull.Value ? "مأموریت " + missionId : reader["Title"].ToString(); driverName = reader["DriverName"].ToString().Trim();
                        }
                    }
                    // Retrying the same action is a no-op, including after completion.
                    if (!(arrived ? alreadyArrived : accepted))
                    {
                        if (arrived ? status != 2 || !accepted : status != 1) throw new InvalidOperationException(arrived ? "رسیدن فقط پس از پذیرش و شروع مأموریت قابل ثبت است." : "این مأموریت قابل پذیرش نیست.");
                        using (var command = new SqlCommand("UPDATE Missions SET " + (arrived ? "ArrivedAtUtc" : "AcceptedAtUtc") + "=SYSUTCDATETIME(), UpdatedAtUtc=SYSUTCDATETIME() WHERE Id=@Id;" + (arrived ? " UPDATE Vehicles SET Speed=0 WHERE Id=(SELECT VehicleId FROM Missions WHERE Id=@Id);" : ""), connection, transaction))
                        { command.Parameters.Add("@Id", SqlDbType.Int).Value = missionId; await command.ExecuteNonQueryAsync(); }
                        notification = await MissionNotificationService.InsertAsync(connection, transaction, missionId, "admin", null,
                            arrived ? "arrived" : "accepted", arrived ? "راننده به مقصد رسید" : "مأموریت پذیرفته شد",
                            (string.IsNullOrWhiteSpace(driverName) ? "راننده " + driverId : driverName) + (arrived ? " رسیدن به مقصد " : " پذیرش ") + title + " را ثبت کرد.");
                    }
                    transaction.Commit();
                }
            }
            await _notifications.PublishAsync(notification);
            var dispatch = await _dispatches.GetByIdAsync(missionId);
            try {
                await _hub.Clients.All.SendAsync("DispatchUpdated", dispatch);
                if (arrived) await _hub.Clients.All.SendAsync("VehicleUpdated", new { id = dispatch.VehicleId });
            } catch { /* Committed records are available through polling. */ }
            return dispatch;
        }
    }
}
