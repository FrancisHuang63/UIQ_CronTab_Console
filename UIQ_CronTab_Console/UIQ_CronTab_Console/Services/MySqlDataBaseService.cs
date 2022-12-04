using Dapper;
using Microsoft.Extensions.Options;
using MySqlConnector;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using UIQ_CronTab_Console;
using UIQ_CronTab_Console.Enums;
using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console.Services
{
    public class MySqlDataBaseService : IDataBaseService
    {
        public string ConnectionString { get; set; }
        public DataBaseEnum DataBase { get; }
        public string DataBaseName { get; set; }
        public string DataBaseUid { get; set; }
        public string DataBasePwd { get; set; }

        public MySqlDataBaseService(IOptions<ConnectoinStringOption> connectoinStringOption, DataBaseEnum dataBase)
        {
            DataBase = dataBase;
            ConnectionString = connectoinStringOption.Value.GetConnectoinString(dataBase);
            using var connection = new MySqlConnection(ConnectionString);
            DataBaseName = connection.Database;

            var builder = new MySqlConnectionStringBuilder(ConnectionString);
            DataBaseUid = builder.UserID;
            DataBasePwd = builder.Password;
        }

        public async Task<int> DeleteAsync(string tableName, object parameter = null)
        {
            var sql = $"DELETE FROM `{tableName}` {GetWhereSql(parameter)}";
            if (parameter == null) return await ExecuteWithTransactionAsync(sql);

            var actualParamter = new DynamicParameters();
            parameter.GetType().GetProperties().ToList().ForEach(prop =>
            {
                actualParamter.Add($"@_{prop.Name}", prop.GetValue(parameter));
            });

            return await ExecuteWithTransactionAsync(sql, actualParamter);
        }

        public async Task<int> ExecuteWithTransactionAsync(string sql, object parameter = null, CommandType commandType = CommandType.Text)
        {
            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var result = await connection.ExecuteAsync(sql, parameter, transaction: transaction, commandType: commandType);
                await transaction.CommitAsync();
                return result;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<IEnumerable<T>> GetAllAsync<T>(string tableName, object parameter = null, string[] selectColumns = null)
        {
            var fieldNames = selectColumns?.Any() ?? false
                ? string.Join(", ", selectColumns.Select(x => $"`{x}`"))
                : string.Join(", ", typeof(T).GetProperties().Where(x => !x.CustomAttributes.Any(a => a.AttributeType == typeof(NotMappedAttribute)))
                    .Select(x => $"`{x.Name.ToLower()}`"));
            var sql = $@"SELECT {fieldNames}
                         FROM `{tableName}` {GetWhereSql(parameter)}";
            if (parameter == null) return await QueryAsync<T>(sql);

            var actualParamter = new DynamicParameters();
            parameter.GetType().GetProperties().ToList().ForEach(prop =>
            {
                actualParamter.Add($"@_{prop.Name}", prop.GetValue(parameter));
            });

            return await QueryAsync<T>(sql, actualParamter);
        }

        public async Task<int> InsertAsync<T>(string tableName, T model)
        {
            if (model == null) return 0;

            var isEnumerable = IsEnumerableType(model.GetType());
            var modelType = isEnumerable ? model.GetType().GetGenericArguments()[0] : model.GetType();

            var props = modelType.GetProperties().Where(x => !x.CustomAttributes.Any(a => a.AttributeType == typeof(NotMappedAttribute) || a.AttributeType == typeof(DatabaseGeneratedAttribute)));
            var fieldNames = props.Select(x => $"`{x.Name.ToLower()}`");
            var fieldValues = props.Select(x => $"@{x.Name}");

            var sql = $@"INSERT INTO `{tableName}`({string.Join(", ", fieldNames)})
                         VALUES({string.Join(", ", fieldValues)})";

            return await ExecuteWithTransactionAsync(sql, model);
        }

        public async Task<long> InsertAndReturnAutoGenerateIdAsync<T>(string tableName, T model)
        {
            if (model == null) return 0;
            var props = model.GetType().GetProperties().Where(x => !x.CustomAttributes.Any(a => a.AttributeType == typeof(NotMappedAttribute) || a.AttributeType == typeof(DatabaseGeneratedAttribute)));
            var fieldNames = props.Select(x => $"`{x.Name.ToLower()}`");
            var fieldValues = props.Select(x => $"@{x.Name}");

            var sql = $@"INSERT INTO `{tableName}`({string.Join(", ", fieldNames)})
                         VALUES({string.Join(", ", fieldValues)});
                         SELECT LAST_INSERT_ID();";

            return (await QueryAsync<long>(sql, model)).FirstOrDefault();
        }

        public async Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameter = null, CommandType commandType = CommandType.Text)
        {
            using var connection = new MySqlConnection(ConnectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                var result = await connection.QueryAsync<T>(sql, parameter, transaction: transaction, commandType: commandType);
                await transaction.CommitAsync();
                return result;
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<int> UpdateAsync<T>(string tableName, T model, object parameter = null)
        {
            if (model == null) return 0;

            var props = model.GetType().GetProperties().Where(x => !x.CustomAttributes.Any(a => a.AttributeType == typeof(NotMappedAttribute) || a.AttributeType == typeof(DatabaseGeneratedAttribute)));
            var setSql = string.Join(", ", props.Select(prop => $"`{prop.Name.ToLower()}` = @{prop.Name}"));
            var whereSql = GetWhereSql(parameter);
            var sql = $@"UPDATE `{tableName}`
                         SET {setSql} {whereSql}";

            var actualParamter = new DynamicParameters();
            props.ToList().ForEach(prop =>
            {
                actualParamter.Add($"@{prop.Name}", prop.GetValue(model));
            });
            parameter.GetType().GetProperties().ToList().ForEach(prop =>
            {
                actualParamter.Add($"@_{prop.Name}", prop.GetValue(parameter));
            });

            return await ExecuteWithTransactionAsync(sql, actualParamter);
        }

        public async Task<int> UpsertAsync<T>(string tableName, T model)
        {
            if (model == null) return 0;
            var isEnumerable = IsEnumerableType(model.GetType());
            var modelType = isEnumerable ? model.GetType().GetGenericArguments()[0] : model.GetType();

            var props = modelType.GetProperties().Where(x => !x.CustomAttributes.Any(a => a.AttributeType == typeof(NotMappedAttribute) || a.AttributeType == typeof(DatabaseGeneratedAttribute)));
            var fieldNames = props.Select(x => $"`{x.Name.ToLower()}`");
            var fieldValues = props.Select(x => $"@{x.Name}");
            var updateSetValues = props.Select(x => $"`{x.Name.ToLower()}` = VALUES({x.Name.ToLower()})");

            var sql = $@"INSERT INTO `{tableName}`({string.Join(", ", fieldNames)})
                         VALUES({string.Join(", ", fieldValues)})
                         ON DUPLICATE KEY
                         UPDATE {string.Join(", ", updateSetValues)}";

            return await ExecuteWithTransactionAsync(sql, model);
        }

        public async Task<bool> IsExistAsync(string tableName, object parameter = null)
        {
            var whereSql = GetWhereSql(parameter);
            var actualParamter = new DynamicParameters();
            parameter.GetType().GetProperties().ToList().ForEach(prop =>
            {
                actualParamter.Add($"@_{prop.Name}", prop.GetValue(parameter));
            });

            var sql = $@"SELECT COUNT(1) FROM `{tableName}` {whereSql}";
            var result = await QueryAsync<int>(sql, actualParamter);
            return result.FirstOrDefault() > 0;
        }

        #region Private Methods

        private bool IsEnumerableType(Type type)
        {
            return typeof(IEnumerable<dynamic>).IsAssignableFrom(type) || type.IsArray;
        }

        private string GetWhereSql(object whereParamter)
        {
            if (whereParamter == null || whereParamter.GetType().GetProperties().Any() == false) return string.Empty;

            var props = whereParamter.GetType().GetProperties().Where(x => !x.CustomAttributes.Any(a => a.AttributeType == typeof(NotMappedAttribute)));
            var whereConditionSqls = props.Select(prop =>
            {
                var isEunmerableType = IsEnumerableType(prop.PropertyType);
                return isEunmerableType
                    ? $"`{prop.Name.ToLower()}` IN {"@_" + prop.Name}"
                    : $"`{prop.Name.ToLower()}` = {"@_" + prop.Name}";
            });

            return whereConditionSqls.Any() ? $"WHERE {string.Join(" AND ", whereConditionSqls)}" : string.Empty;
        }

        #endregion Private Methods
    }
}