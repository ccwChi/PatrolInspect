using Dapper;
using PatrolInspect.Models;
using Microsoft.Extensions.Options;
using PatrolInspect.Repositories.Interfaces;
using System.Data;
using Microsoft.Data.SqlClient; 

namespace PatrolInspect.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly string _mesConn;
        private readonly ILogger<UserRepository> _logger;

        public UserRepository(IConfiguration configuration, IOptions<AppSettings> appSettings, ILogger<UserRepository> logger)
        {
            _logger = logger;

            // 根據 EnvFlag 取得對應的連線字串
            var envFlag = appSettings.Value.EnvFlag;
            var connectionKey = EnvironmentHelper.GetMesConnectionStringKey(envFlag);
            _mesConn = configuration.GetConnectionString(connectionKey)
                ?? throw new ArgumentNullException($"ConnectionString '{connectionKey}' not found");

            var envName = EnvironmentHelper.GetEnvironmentName(envFlag);
            _logger.LogInformation("UserRepository initialized with Environment: {EnvName} (EnvFlag: {EnvFlag})", envName, envFlag);
        }

        private IDbConnection CreateConnection()
        {
            return new SqlConnection(_mesConn);
        }

        public async Task<MesUser?> GetUserByUserNoAsync(string userNo)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT 
                    mu.UserNo, 
                    mu.UserName, 
                    mu.DepartmentName, 
                    md.FatherDepartmentName, 
                    mu.TitleName, 
                    mu.ExpirationDate 
                FROM MES_USERS mu
                LEFT JOIN MES_DEPARTMENT md ON mu.DepartmentNo = md.DepartmentNo
                WHERE mu.UserNo = @UserNo";

            try
            {
                var user = await connection.QueryFirstOrDefaultAsync<MesUser>(sql, new { UserNo = userNo });

                return user;
            }
            catch (Exception ex)
            {
                throw;
            }
        }

        public async Task<(bool Success, string Message, MesUser? User)> ValidateUserLoginAsync(string userNo)
        {
            try
            {
                var user = await GetUserByUserNoAsync(userNo);

                if (user == null)
                {
                    return (false, "工號不存在，請確認輸入正確", null);
                }

                if (!user.IsActive)
                {
                    return (false, "此帳號已離職，無法登入系統", null);
                }

                return (true, "登入成功", user);
            }
            catch (Exception ex)
            {
                return (false, "系統連線異常，請稍後再試或聯繫IT部門", null);
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                using var connection = CreateConnection();
                await connection.QueryFirstOrDefaultAsync<int>("SELECT 1");
                _logger.LogInformation("Database connection test successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Database connection test failed");
                return false;
            }
        }
    }
}