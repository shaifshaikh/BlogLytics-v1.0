using Microsoft.Data.SqlClient;
using System.Data;

namespace Bloglytics.Connection
{
    public class DBConnection
    {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public DBConnection(IConfiguration configuration)
        {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("SqlConnection");
        }

        public IDbConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }

    }
}
