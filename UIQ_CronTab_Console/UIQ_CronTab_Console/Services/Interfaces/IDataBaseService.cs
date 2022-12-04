using System.Data;
using UIQ_CronTab_Console.Enums;

namespace UIQ_CronTab_Console.Services.Interfaces
{
    public interface IDataBaseService
    {
        DataBaseEnum DataBase { get; }
        string ConnectionString { get; set; }
        string DataBaseName { get; set; }
        public string DataBaseUid { get; set; }
        public string DataBasePwd { get; set; }

        public Task<int> DeleteAsync(string tableName, object parameter = null);

        public Task<IEnumerable<T>> GetAllAsync<T>(string tableName, object parameter = null, string[] selectColumns = null);

        public Task<int> InsertAsync<T>(string tableName, T model);

        public Task<long> InsertAndReturnAutoGenerateIdAsync<T>(string tableName, T model);

        public Task<int> UpdateAsync<T>(string tableName, T model, object parameter = null);

        public Task<int> UpsertAsync<T>(string tableName, T model);

        public Task<bool> IsExistAsync(string tableName, object parameter = null);

        public Task<int> ExecuteWithTransactionAsync(string sql, object parameter = null, CommandType commandType = CommandType.Text);

        public Task<IEnumerable<T>> QueryAsync<T>(string sql, object parameter = null, CommandType commandType = CommandType.Text);
    }
}