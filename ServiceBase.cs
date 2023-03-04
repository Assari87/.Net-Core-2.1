using Dapper;
using Microsoft.AspNetCore.Identity;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace Check.Core.Services
{
    public abstract class ServiceBase
    {
        public UserManager<User> UserManager { get; set; }

        public static string DefaultConnectionString { get; set; }
        public static string WorkflowConnectionString { get; set; }

        public ServiceBase(UserManager<User> userManager)
        {
            this.UserManager = userManager;
        }

        public static IQueryable<T> Query<T>(string sql, object param = null, CommandType commandType = CommandType.Text)
        {
            using (var connection = new SqlConnection(DefaultConnectionString))
            {
                return connection.Query<T>(sql, param, commandType: commandType).AsQueryable();
            }
        }
        public static IQueryable Query(string sql, object param = null, CommandType commandType = CommandType.Text)
        {
            using (var connection = new SqlConnection(DefaultConnectionString))
            {
                return connection.Query(sql, param, commandType: commandType).AsQueryable();
            }
        }

        public static IQueryable<T> Queryable<T>(string sql, object param = null, CommandType commandType = CommandType.Text)
        {
            using (var connection = new SqlConnection(DefaultConnectionString))
            {
                return connection.Query<T>(sql, param, commandType: commandType).AsQueryable();
            }
        }

        public static IQueryable<T> Query<T>(Func<T> typeBuilder, string sql, object param = null, CommandType commandType = CommandType.Text)
        {
            using (var connection = new SqlConnection(DefaultConnectionString))
            {
                return connection.Query<T>(sql, param, commandType: commandType).AsQueryable();
            }
        }

        public static void ExecuteQuery(string query, object param = null)
        {
            using (IDbConnection dbConnection = new SqlConnection(DefaultConnectionString))
            {
                dbConnection.Open();
                dbConnection.Execute(query, param);
                dbConnection.Close();
            }
        }
    }
}
