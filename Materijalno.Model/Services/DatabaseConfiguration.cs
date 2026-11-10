using System;
using System.Configuration;
using System.Data.SqlClient;

namespace Materijalno.Model.Services
{
    public static class DatabaseConfiguration
    {
        public static string MaterialConnectionString
        {
            get
            {
                var setting = ConfigurationManager.ConnectionStrings["MaterijalnoDb"];
                if (setting == null) throw new InvalidOperationException("Nedostaje konfiguracija MaterijalnoDb.");
                var builder = new SqlConnectionStringBuilder(setting.ConnectionString);
                if (!builder.InitialCatalog.StartsWith("materijalno_fox_", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Aplikacija zahtijeva novu Fox bazu. Postojeća baza nije otvorena.");
                return builder.ConnectionString;
            }
        }

        public static string OracleConnectionString
        {
            get
            {
                var setting = ConfigurationManager.ConnectionStrings["OracleFinansije"];
                if (setting == null) throw new InvalidOperationException("Nedostaje konfiguracija OracleFinansije.");
                return setting.ConnectionString;
            }
        }

        public static bool OracleExportEnabled =>
            string.Equals(ConfigurationManager.AppSettings["OracleExportEnabled"], "true", StringComparison.OrdinalIgnoreCase);
    }
}
