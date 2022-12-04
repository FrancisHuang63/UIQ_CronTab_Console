using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using UIQ_CronTab_Console.Models;
using UIQ_CronTab_Console.Models.ApiRequest;
using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console.Services
{
    public class PhaseLogService : IPhaseLogService
    {
        private readonly ISshCommandService _sshCommandService;
        private readonly IDataBaseService _dataBaseNcsUiService;
        private readonly ILogFileService _logFileService;

        private readonly string _uiPath;

        public PhaseLogService(ISshCommandService sshCommandService, IEnumerable<IDataBaseService> dataBaseServices
            , ILogFileService logFileService, IConfiguration configuration)
        {
            _sshCommandService = sshCommandService;
            _dataBaseNcsUiService = dataBaseServices.Single(x => x.DataBase == Enums.DataBaseEnum.NcsUi);
            _logFileService = logFileService;

            _uiPath = configuration.GetValue<string>("UiPath");
        }

        public async Task PhaseLogAsync(PhaseLogRequest request)
        {
            //取得昨天之日期
            var date = DateTime.Now.TWNow().AddDays(-1).Date;
            var year = DateTime.Now.TWNow().AddDays(-1).Year;

            //可由參數設定重跑之日期
            if (request.FileDate.HasValue)
            {
                year = request.FileDate.Value.Date > DateTime.Now.TWNow().AddDays(-1).Date
                    ? DateTime.Now.TWNow().AddYears(-1).Year
                    : year;
            }

            var fileName = $"job{date.ToString("MMdd")}";
            IEnumerable<string> dataList;
            var i = 0;

            var all_acc_path = (await _sshCommandService.RunCommandAsync($"sh {_uiPath}shell/logPath.sh")).Split(" ");

            foreach (var item in all_acc_path)
            {
                var acc_path = item.TrimEnd();

                var valarr = acc_path.Split("/");
                var modelName = valarr[3];
                var memberName = valarr[4];
                var logPath = acc_path + "/log/";
                dataList = await ReadFileAsync(modelName, memberName, logPath, fileName, date, year);
                if (dataList?.Any() ?? false)
                {
                    foreach (var row in dataList)
                    {
                        var sqls = row.Split(";");
                        foreach (var sql in sqls)
                        {
                            if (!Regex.IsMatch(sql, "/^\\s*$/"))
                            {
                                await _dataBaseNcsUiService.ExecuteWithTransactionAsync(sql);
                            }
                        }
                    }
                }
            }

            //清除180天之資料
            await DeleteOldLogAsync();
        }

        #region Private Method

        private async Task DeleteOldLogAsync()
        {
            var date = DateTime.Now.TWNow().AddDays(-180);
            var sql = "Delete From `history_batch` Where `start_time` < @Date";
            await _dataBaseNcsUiService.ExecuteWithTransactionAsync(sql, new { Date = date });
        }

        private async Task<IEnumerable<string>> ReadFileAsync(string modelName, string memberName, string logPath, string fileName, DateTime date, int year)
        {
            if (File.Exists(logPath + fileName) == false)
            {
                //echo "File not found:" + model + " " + member + " " + logPath + fileName + "<br>\n";
                return new string[] { };
            }

            var find = "]";
            var findStageStart = "stage_start";
            var findStageDone = "stage_done";
            var findStageFail = "fail";
            var findStageCancel = "Cancelled";
            var replaceString = "No Status";

            var fileStartTime = string.Empty;
            var tagTime = string.Empty;
            var type = string.Empty;
            var id = string.Empty;
            var dtg = string.Empty;
            var status = string.Empty;
            var memberStartTime = string.Empty;
            var memberDoneTime = string.Empty;
            var stageStartTime = string.Empty;
            var stageDoneTime = string.Empty;
            var stageName = string.Empty;

            var dataRow = new List<string>();       //新增新資料

            //echo "Exec File:" + model + " " + member + " " + logPath + fileName + "<br>\n";
            var logContent = await _logFileService.ReadLogFileAsync(logPath + fileName);
            if (string.IsNullOrWhiteSpace(logContent)) return new string[] { };

            //資料大於一筆以上才處理
            var contents = (logContent ?? string.Empty).Split("\n");
            foreach (var Line in contents)
            {
                if (Line.Length <= 8) continue;

                //單筆長度因大於8避免異常情況
                //判斷是否為標題列
                var values = Line.Split(" ");
                if (Line.Contains(find) == false)
                {
                    memberStartTime = string.Empty;
                    memberDoneTime = string.Empty;

                    //因有無Major or Post 長度不同
                    if (values.Count() == 3)
                    {
                        id = values[2];
                    }
                    else if (values.Count() == 4)
                    {
                        type = values[2];
                        id = values[3];
                    }
                    dtg = id.Substring(6);
                }
                else
                {
                    //取得此筆內容時間
                    tagTime = values[0].Replace("[", string.Empty).Replace("]", string.Empty);
                    var nowDatePrefix = DateTime.Now.ToString("yyyy/MM/dd");

                    //起始時間 > 現在時間 表跨天
                    if (fileStartTime == string.Empty)
                    {
                        fileStartTime = tagTime;
                    }
                    else if (DateTime.Parse($"{nowDatePrefix} {fileStartTime}") > DateTime.Parse($"{nowDatePrefix} {tagTime}"))
                    {
                        date = date.AddDays(1).Date;
                        fileStartTime = tagTime;
                    }

                    //取得stage_start列
                    if (Line.Contains(findStageStart))
                    {
                        memberStartTime = memberStartTime == string.Empty ? tagTime : memberStartTime;
                        stageStartTime = values.Count() >= 3 ? stageName = values[2] : tagTime;

                        continue;
                    }

                    //取得stage_done列
                    if (Line.Contains(findStageDone))
                    {
                        stageDoneTime = tagTime;

                        dataRow.Add(await GetInsertSqlAsync(modelName, memberName, type, id, dtg, stageName, replaceString, stageStartTime, stageDoneTime, date, year));
                        continue;
                    }

                    //取得stage_fail列
                    if (Line.Contains(findStageFail))
                    {
                        stageDoneTime = tagTime;
                        dataRow.Add(await GetInsertSqlAsync(modelName, memberName, type, id, dtg, stageName, findStageFail, stageStartTime, stageDoneTime, date, year));
                        continue;
                    }

                    //取得stage_cancel列
                    if (Line.Contains(findStageCancel))
                    {
                        stageDoneTime = tagTime;
                        dataRow.Add(await GetInsertSqlAsync(modelName, memberName, type, id, dtg, stageName, findStageCancel, stageStartTime, stageDoneTime, date, year));
                        continue;
                    }

                    //取得status列
                    if (Line.Contains($"{modelName} {memberName} {id}"))
                    {
                        if (values.Count() < 6) continue;

                        memberDoneTime = tagTime;
                        status = values[5];
                        dataRow.ForEach(x => x.Replace(replaceString, status));
                        dataRow.Add(await InsertMemberSqlAsync(modelName, memberName, type, id, dtg, string.Empty, status, memberStartTime, memberDoneTime, date, year));
                        continue;
                    }
                }
            }
            return dataRow;
        }

        /// <summary>
        /// 新增model資料列
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="modelName2"></param>
        /// <param name="type"></param>
        /// <param name="id"></param>
        /// <param name="dtg"></param>
        /// <param name="stageName"></param>
        /// <param name="status"></param>
        /// <param name="stageStartTime"></param>
        /// <param name="stageDoneTime"></param>
        /// <param name="date"></param>
        /// <param name="year"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<string> InsertMemberSqlAsync(string modelName, string memberName, string type, string id, string dtg, string stageName, string status, string stageStartTime, string stageDoneTime, DateTime date, int year)
        {
            var nowDatePrefix = DateTime.Now.ToString("yyyy/MM/dd");
            var startDate = date;
            var stageStartDateTime = DateTime.Parse($"{nowDatePrefix} {stageStartTime}");
            var stageDoneDateTime = DateTime.Parse($"{nowDatePrefix} {stageDoneTime}");
            var eYear = 0;

            if (stageStartDateTime > stageDoneDateTime)
                startDate = startDate.AddDays(-1).Date;

            var startTime = new DateTime(year, startDate.Month, startDate.Day
                , stageStartDateTime.Hour, stageStartDateTime.Minute, stageStartDateTime.Second).ToString("yyyy/MM/dd HH:mm:ss");

            eYear = startDate > date ? year + 1 : year;

            var endTime = new DateTime(eYear, date.Month, date.Day
                , stageDoneDateTime.Hour, stageDoneDateTime.Minute, stageDoneDateTime.Second).ToString("yyyy/MM/dd HH:mm:ss");

            var orgType = type.ToString();
            var orgDtg = dtg.ToString();

            var typeMP = type.Split("_").FirstOrDefault();
            var typeRes = await CheckTypeAsync(modelName, memberName, typeMP, stageName);
            typeMP = typeRes.Any() ? typeMP : string.Empty;

            var dtgRes = await CheckDtgAsync(modelName, memberName, typeMP, stageName, dtg);
            dtg = dtgRes.Any() ? dtg : string.Empty;

            var rtval = $"Delete From `history_batch` Where `history_batch_type` = 'model' And `model` = '{modelName}' And `member` = '{memberName}' And `batch_name` = '{stageName}' And `id` = '{id}' And `start_time` = '{startTime}' And `end_time` = '{endTime}';";
            rtval += $"Insert into `history_batch` values (null, 'model','{modelName}','{memberName}','{typeMP}','{stageName}','{id}','{dtg}','{orgType}','{orgDtg}','{status}','{startTime}','{endTime}');";
            return rtval;
        }

        private async Task<string> GetInsertSqlAsync(string modelName, string memberName, string type, string id, string dtg, string stageName, string status, string stageStartTime, string stageDoneTime, DateTime date, int year)
        {
            var nowDatePrefix = DateTime.Now.ToString("yyyy/MM/dd");
            var startDate = date;
            var stageStartDateTime = DateTime.Parse($"{nowDatePrefix} {stageStartTime}");
            var stageDoneDateTime = DateTime.Parse($"{nowDatePrefix} {stageDoneTime}");
            var eYear = 0;

            if (stageStartDateTime > stageDoneDateTime)
                startDate = startDate.AddDays(-1).Date;

            var startTime = new DateTime(year, startDate.Month, startDate.Day
                , stageStartDateTime.Hour, stageStartDateTime.Minute, stageStartDateTime.Second).ToString("yyyy/MM/dd HH:mm:ss");
            eYear = startDate > date ? year + 1 : year;

            var endTime = new DateTime(eYear, date.Month, date.Day
                , stageDoneDateTime.Hour, stageDoneDateTime.Minute, stageDoneDateTime.Second).ToString("yyyy/MM/dd HH:mm:ss");

            var orgType = type.ToString();
            var orgDtg = dtg.ToString();

            var typeMP = type.Split("_").FirstOrDefault();
            var typeRes = await CheckTypeAsync(modelName, memberName, typeMP, stageName);
            typeMP = typeRes.Any() ? typeMP : string.Empty;

            var dtgRes = await CheckDtgAsync(modelName, memberName, typeMP, stageName, dtg);
            dtg = dtgRes.Any() ? dtg : string.Empty;

            var rtval = $"Delete From `history_batch` Where `history_batch_type` = 'stage' And `model` = '{modelName}' And `member` = '{memberName}' And `batch_name` = '{stageName}' And `id` = '{id}' And `start_time` = '{startTime}' And `end_time` = '{endTime}';";
            rtval += $"Insert into `history_batch` values (null, 'stage','{modelName}','{memberName}','{typeMP}','{stageName}','{id}','{dtg}','{orgType}','{orgDtg}','{status}','{startTime}','{endTime}');";

            return rtval;
        }

        private async Task<IEnumerable<BatchView>> CheckDtgAsync(string modelName, string memberName, string? typeMP, string stageName, string dtg)
        {
            var sql = @"SELECT * FROM `batch_view`
                        WHERE `model` = @Model
                        AND `member` = @Member
                        AND `batch` = @Batch
                        AND `type` = @Type
                        AND `dtg` = @Dtg;";
            var param = new
            {
                Model = modelName,
                MemberName = memberName,
                Type = typeMP,
                Batch = stageName,
                Dtg = dtg,
            };

            return await _dataBaseNcsUiService.QueryAsync<BatchView>(sql, param);
        }

        private async Task<IEnumerable<BatchView>> CheckTypeAsync(string modelName, string memberName, string? typeMP, string stageName)
        {
            var sql = @"SELECT * FROM `batch_view`
                        WHERE `model` = @Model
                        AND `member` = @Member
                        AND `batch` = @Batch
                        AND `type` = @Type;";
            var param = new
            {
                Model = modelName,
                MemberName = memberName,
                Type = typeMP,
                Batch = stageName,
            };

            return await _dataBaseNcsUiService.QueryAsync<BatchView>(sql, param);
        }

        #endregion Private Method
    }
}