using System.Data;
using System.Data.SqlClient;
namespace MorosidadWeb.Data {
    public class DapperContext {
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly string _connection10;
        public DapperContext(IConfiguration configuration) {
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("FemacoConnection");
            _connection10 = _configuration.GetConnectionString("Femaco10");
        }
        public IDbConnection CreateConnection() => new SqlConnection(_connectionString);
        public IDbConnection CreateConnection10() => new SqlConnection(_connection10);
    }
}