namespace PatrolInspect.Models
{
    public class AppSettings
    {
        public int EnvFlag { get; set; }
        public int SessionTimeout { get; set; }
        public bool NfcValidationEnabled { get; set; }
        public int MaxScanAttempts { get; set; }
        public Dictionary<string, string> Environment { get; set; } = new();
    }

    public enum EnvironmentType
    {
        Development = 0,
        Production = 1
    }

    public static class EnvironmentHelper
    {
        public static string GetMesConnectionStringKey(int envFlag)
        {
            return envFlag switch
            {
                0 => "Mes_DevConn",
                1 => "MesConn",
                _ => "Mes_DevConn"
            };
        }

        public static string GetFnReportConnectionStringKey(int envFlag)
        {
            return envFlag switch
            {
                0 => "FnReportConn",
                1 => "FnReportConn",
                _ => "FnReportConn",
            };
        }

        public static string GetEnvironmentName(int envFlag)
        {
            return envFlag switch
            {
                0 => "Development",
                1 => "Production",
                _ => "Development"
            };
        }

        public static bool IsProduction(int envFlag)
        {
            return envFlag == 1;
        }

        public static bool IsDevelopment(int envFlag)
        {
            return envFlag == 0;
        }
    }
}
