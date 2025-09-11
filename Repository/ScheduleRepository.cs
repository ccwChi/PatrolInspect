using Dapper;
using PatrolInspect.Models;
using PatrolInspect.Models.Entities;
using Microsoft.Extensions.Options;
using PatrolInspect.Repositories.Interfaces;
using System.Data;
using Microsoft.Data.SqlClient;

namespace PatrolInspect.Repository
{
    public class ScheduleRepository : IScheduleRepository
    {
        private readonly string _connectionString;
        private readonly ILogger<ScheduleRepository> _logger;

        public ScheduleRepository(IConfiguration configuration, IOptions<AppSettings> appSettings, ILogger<ScheduleRepository> logger)
        {
            _logger = logger;
            var envFlag = appSettings.Value.EnvFlag;
            var connectionKey = EnvironmentHelper.GetMesConnectionStringKey(envFlag);
            _connectionString = configuration.GetConnectionString(connectionKey)
                ?? throw new ArgumentNullException($"ConnectionString '{connectionKey}' not found");
        }

        private IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }


        public async Task<ScheduleBaseInfo> GetScheduleBaseInfoAsync()
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT distinct UserName FROM INSPECTION_SCHEDULE_EVENT ORDER BY UserName asc;
                SELECT distinct Department FROM INSPECTION_SCHEDULE_EVENT order by Department asc;
                SELECT distinct Area FROM INSPECTION_DEVICE_AREA_MAPPING order by Area asc
                SELECT EventType FROM INSPECTION_EVENT_TYPE_MASTER WHERE IsActive = 1 ORDER BY EventType
                SELECT 
                    mu.UserNo, mu.UserName, mu.DepartmentName as Department, md.FatherDepartmentName as FatherDepartment, mu.TitleName
                FROM MES_USERS mu
                LEFT JOIN MES_DEPARTMENT md ON mu.DepartmentNo = md.DepartmentNo
                WHERE (mu.ExpirationDate IS NULL OR mu.ExpirationDate > GETDATE())
                ORDER BY mu.UserNo";

            try
            {
                using var multi = await connection.QueryMultipleAsync(sql);

                var scheduleUsers = (await multi.ReadAsync<string>()).ToList();
                var scheduleDeparts = (await multi.ReadAsync<string>()).ToList();
                var areas = (await multi.ReadAsync<string>()).ToList();
                var eventTypes = (await multi.ReadAsync<string>()).ToList();
                var allUsers = (await multi.ReadAsync<MesUserDto>()).ToList();

                return new ScheduleBaseInfo
                {
                    ScheduleUserNames = scheduleUsers,
                    ScheduleDepartments = scheduleDeparts,
                    Areas = areas,
                    EventTypes = eventTypes,
                    Users = allUsers
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department from INSPECTION_SCHEDULE_EVENT");
                throw;
            }
        }

        public async Task<List<InspectionScheduleEvent>> GetSearchSchedules(string userName, string depart, DateTime? startDate, DateTime? endDate)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT EventId, UserNo, UserName, Department, EventType, EventDetail, 
                       StartDateTime, EndDateTime, Area
                FROM INSPECTION_SCHEDULE_EVENT 
                WHERE 1=1 ";


            if (!string.IsNullOrWhiteSpace(userName))
            {
                sql += @"and UserName = @userName ";
            }

            if (!string.IsNullOrWhiteSpace(depart))
            {
                sql += @"and Department = @depart ";
            }


            sql += @"and StartDateTime >= @startDate and EndDateTime <=  @endDate
                    ORDER BY StartDateTime desc, userName asc;";


            try
            {
                var schedules = await connection.QueryAsync<InspectionScheduleEvent>(sql, new { userName, depart, startDate, endDate });
                return schedules.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schedules for user: {userName}", userName);
                throw;
            }
        }



        public async Task<List<int>> CreateSchedulesBatchAsync(List<InspectionScheduleEvent> schedules)
        {
            await using var connection = (SqlConnection)CreateConnection();
            await connection.OpenAsync(); // ← 先打開

            await using var transaction = await connection.BeginTransactionAsync();
            try
            {
                const string sql = @"
                    INSERT INTO INSPECTION_SCHEDULE_EVENT 
                    (UserNo, UserName, Department, EventType, EventDetail, StartDateTime, EndDateTime, 
                     Area, CreateDate, CreateBy)
                    VALUES 
                    (@UserNo, @UserName,@Department, @EventType, @EventDetail, @StartDateTime, @EndDateTime, 
                     @Area, @CreateDate, @CreateBy);
                    SELECT CAST(SCOPE_IDENTITY() as int);";

                var now = DateTime.Now;
                var eventIds = new List<int>(schedules.Count);

                foreach (var schedule in schedules)
                {
                    schedule.CreateDate = now;
                    var eventId = await connection.QuerySingleAsync<int>(
                        sql,
                        schedule,
                        transaction  
                    );
                    eventIds.Add(eventId);
                }

                await transaction.CommitAsync();
                _logger.LogInformation("Batch created {Count} schedule events", schedules.Count);
                return eventIds;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error batch creating {Count} schedules", schedules.Count);
                throw;
            }
        }


        public async Task<bool> DeleteSchedulesBatchAsync(List<int> eventIds)
        {
            using var connection = CreateConnection();
            var sql = "DELETE FROM INSPECTION_SCHEDULE_EVENT WHERE EventId IN @EventIds";

            try
            {
                var rowsAffected = await connection.ExecuteAsync(sql, new { EventIds = eventIds });
                _logger.LogInformation("Batch deleted {Count} schedule events", eventIds.Count);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error batch deleting {Count} schedules", eventIds.Count);
                throw;
            }
        }


    }
}