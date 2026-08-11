using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;

namespace sunpath.Data
{
    public class DbHelper
    {
            private readonly string _connectionString;
            public DbHelper(IConfiguration configuration)
            {
                _connectionString = configuration.GetConnectionString("SunPathConnection");
            }
            public SqlConnection GetConnection()
            {
                return new SqlConnection(_connectionString);
            }
      
    }
}
