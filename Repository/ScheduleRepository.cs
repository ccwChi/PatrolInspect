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

        public async Task<List<InspectionScheduleEvent>> GetAllSchedulesAsync()
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT EventId, UserNo, UserName, EventType, EventDetail, 
                       StartDateTime, EndDateTime, Area, IsActive, 
                       CreateDate, CreateBy, UpdateDate, UpdateBy
                FROM INSPECTION_SCHEDULE_EVENT 
                ORDER BY StartDateTime DESC";

            try
            {
                var schedules = await connection.QueryAsync<InspectionScheduleEvent>(sql);
                return schedules.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all schedules");
                throw;
            }
        }

        public async Task<List<InspectionScheduleEvent>> GetSchedulesByUserAsync(string userNo)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT EventId, UserNo, UserName, EventType, EventDetail, 
                       StartDateTime, EndDateTime, Area, IsActive, 
                       CreateDate, CreateBy, UpdateDate, UpdateBy
                FROM INSPECTION_SCHEDULE_EVENT 
                WHERE UserNo = @UserNo 
                ORDER BY StartDateTime";

            try
            {
                var schedules = await connection.QueryAsync<InspectionScheduleEvent>(sql, new { UserNo = userNo });
                return schedules.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schedules for user: {UserNo}", userNo);
                throw;
            }
        }

        public async Task<List<InspectionScheduleEvent>> GetSchedulesByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT EventId, UserNo, UserName, EventType, EventDetail, 
                       StartDateTime, EndDateTime, Area, IsActive, 
                       CreateDate, CreateBy, UpdateDate, UpdateBy
                FROM INSPECTION_SCHEDULE_EVENT 
                WHERE StartDateTime >= @StartDate AND StartDateTime <= @EndDate
                ORDER BY StartDateTime";

            try
            {
                var schedules = await connection.QueryAsync<InspectionScheduleEvent>(sql,
                    new { StartDate = startDate, EndDate = endDate });
                return schedules.ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schedules for date range: {StartDate} - {EndDate}", startDate, endDate);
                throw;
            }
        }

        public async Task<int> CreateScheduleAsync(InspectionScheduleEvent schedule)
        {
            using var connection = CreateConnection();
            var sql = @"
                INSERT INTO INSPECTION_SCHEDULE_EVENT 
                (UserNo, UserName, EventType, EventDetail, StartDateTime, EndDateTime, 
                 Area, IsActive, CreateDate, CreateBy)
                VALUES 
                (@UserNo, @UserName, @EventType, @EventDetail, @StartDateTime, @EndDateTime, 
                 @Area, @IsActive, @CreateDate, @CreateBy);
                SELECT CAST(SCOPE_IDENTITY() as int)";

            try
            {
                schedule.CreateDate = DateTime.Now;
                var eventId = await connection.QuerySingleAsync<int>(sql, schedule);
                _logger.LogInformation("Created schedule event: {EventId} for user: {UserNo}", eventId, schedule.UserNo);
                return eventId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating schedule for user: {UserNo}", schedule.UserNo);
                throw;
            }
        }

        public async Task<List<int>> CreateSchedulesBatchAsync(List<InspectionScheduleEvent> schedules)
        {
            using var connection = CreateConnection();
            using var transaction = connection.BeginTransaction();

            try
            {
                var sql = @"
                    INSERT INTO INSPECTION_SCHEDULE_EVENT 
                    (UserNo, UserName, EventType, EventDetail, StartDateTime, EndDateTime, 
                     Area, IsActive, CreateDate, CreateBy)
                    VALUES 
                    (@UserNo, @UserName, @EventType, @EventDetail, @StartDateTime, @EndDateTime, 
                     @Area, @IsActive, @CreateDate, @CreateBy);
                    SELECT CAST(SCOPE_IDENTITY() as int)";

                var eventIds = new List<int>();
                foreach (var schedule in schedules)
                {
                    schedule.CreateDate = DateTime.Now;
                    var eventId = await connection.QuerySingleAsync<int>(sql, schedule, transaction);
                    eventIds.Add(eventId);
                }

                transaction.Commit();
                _logger.LogInformation("Batch created {Count} schedule events", schedules.Count);
                return eventIds;
            }
            catch (Exception ex)
            {
                transaction.Rollback();
                _logger.LogError(ex, "Error batch creating {Count} schedules", schedules.Count);
                throw;
            }
        }

        public async Task<bool> UpdateScheduleAsync(InspectionScheduleEvent schedule)
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE INSPECTION_SCHEDULE_EVENT 
                SET UserNo = @UserNo, UserName = @UserName, EventType = @EventType, 
                    EventDetail = @EventDetail, StartDateTime = @StartDateTime, 
                    EndDateTime = @EndDateTime, Area = @Area, IsActive = @IsActive,
                    UpdateDate = @UpdateDate, UpdateBy = @UpdateBy
                WHERE EventId = @EventId";

            try
            {
                schedule.UpdateDate = DateTime.Now;
                var rowsAffected = await connection.ExecuteAsync(sql, schedule);
                _logger.LogInformation("Updated schedule event: {EventId}", schedule.EventId);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating schedule: {EventId}", schedule.EventId);
                throw;
            }
        }

        public async Task<bool> DeleteScheduleAsync(int eventId)
        {
            using var connection = CreateConnection();
            var sql = "DELETE FROM INSPECTION_SCHEDULE_EVENT WHERE EventId = @EventId";

            try
            {
                var rowsAffected = await connection.ExecuteAsync(sql, new { EventId = eventId });
                _logger.LogInformation("Deleted schedule event: {EventId}", eventId);
                return rowsAffected > 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting schedule: {EventId}", eventId);
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

        public async Task<InspectionScheduleEvent?> GetScheduleByIdAsync(int eventId)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT EventId, UserNo, UserName, EventType, EventDetail, 
                       StartDateTime, EndDateTime, Area, IsActive, 
                       CreateDate, CreateBy, UpdateDate, UpdateBy
                FROM INSPECTION_SCHEDULE_EVENT 
                WHERE EventId = @EventId";

            try
            {
                var schedule = await connection.QueryFirstOrDefaultAsync<InspectionScheduleEvent>(sql, new { EventId = eventId });
                return schedule;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting schedule: {EventId}", eventId);
                throw;
            }
        }
    }
}