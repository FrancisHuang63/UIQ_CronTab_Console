using Microsoft.Extensions.Configuration;
using System.Text.RegularExpressions;
using UIQ_CronTab_Console.Models;
using UIQ_CronTab_Console.Services.Interfaces;

namespace UIQ_CronTab_Console.Services
{
    public class ParseLogService : IParseLogService
    {
        private readonly ISshCommandService _sshCommandService;
        private readonly IDataBaseService _dataBaseNcsUiService;
        private readonly IDataBaseService _dataBaseNcsLogService;
        private readonly ILogFileService _logFileService;

        private readonly string _systemName;
        private readonly string _shellPath;
        private readonly string _uiPath;

        public ParseLogService(ISshCommandService sshCommandService, IEnumerable<IDataBaseService> dataBaseServices
            , ILogFileService logFileService, IConfiguration configuration)
        {
            _sshCommandService = sshCommandService;
            _dataBaseNcsUiService = dataBaseServices.Single(x => x.DataBase == Enums.DataBaseEnum.NcsUi);
            _dataBaseNcsLogService = dataBaseServices.Single(x => x.DataBase == Enums.DataBaseEnum.NcsLog);
            _logFileService = logFileService;

            _systemName = configuration.GetValue<string>("SystemName");
            _shellPath = configuration.GetValue<string>("ShellPath");
            _uiPath = configuration.GetValue<string>("UiPath");
        }

        public async Task ParseLogAsync()
        {
            var allAccPath = await _sshCommandService.RunCommandAsync($"sh {_uiPath}shell/log_path.sh");
            await _sshCommandService.RunCommandAsync($"{_shellPath}log_collector.ksh " + allAccPath);
            var allMemberInfoDatas = await _sshCommandService.RunCommandAsync($"{_shellPath}get_all_member_info.ksh {allAccPath}");
            var allMemberInfo = ParseString(allMemberInfoDatas, true);
            var model_cfg = await GetModelConfigAsync();
            var batchConfig = await GetBatchConfigAsync();
            var logPath = $"{_uiPath}log";

            var monitoringInfos = new List<MonitoringInfo>();
            foreach (var info in model_cfg)
            {
                var monitoringInfo = new MonitoringInfo
                {
                    Model = info.Model_Name,
                    Nickname = info.Nickname,
                    Member = info.Member_Name,
                    Account = info.Account,
                };

                var logFilePath = $"{logPath}/{info.Model_Name}/{info.Nickname}/{info.Member_Name}.log";
                if (File.Exists(logFilePath) == false)
                {
                    monitoringInfo.Comment = "no log";
                    continue;
                }

                GenerateMonitoringInfo(monitoringInfo, info, allMemberInfo);
                await SetMonitoringInfoAsync(monitoringInfo, info, batchConfig, logFilePath);

                monitoringInfos.Add(monitoringInfo);
            }

            UpdateMonitoringInfo(monitoringInfos);
        }

        #region Private Method

        private void UpdateMonitoringInfo(IEnumerable<MonitoringInfo> monitoringInfo)
        {
            _dataBaseNcsUiService.UpsertAsync("monitoring_info", monitoringInfo);
        }

        private void GenerateMonitoringInfo(MonitoringInfo monitoringInfo, ModelMember modelMember, Dictionary<string, Dictionary<string, string>> allMemberInfo)
        {
            var path = modelMember.Member_Path ?? string.Empty;
            var key = $"/{_systemName}/{modelMember.Account}{path}/{modelMember.Model_Name}/{modelMember.Member_Name}";
            int.TryParse(allMemberInfo[key][nameof(MonitoringInfo.Lid)], out var lid);
            monitoringInfo.Lid = lid;

            monitoringInfo.Dtg = allMemberInfo[$"/{_systemName}/{modelMember.Account}{path}/{modelMember.Model_Name}/{modelMember.Member_Name}"][nameof(MonitoringInfo.Dtg)];
            monitoringInfo.Run = monitoringInfo.Dtg.Substring(6, 2);
            monitoringInfo.Complete_Run_Type = allMemberInfo[key][nameof(MonitoringInfo.Run)];
            monitoringInfo.Run_Type = monitoringInfo.Complete_Run_Type == "Default" ? string.Empty : monitoringInfo.Complete_Run_Type.Split("_").FirstOrDefault();
            monitoringInfo.Cron_Mode = allMemberInfo[key][nameof(MonitoringInfo.Cron_Mode)];

            int.TryParse(allMemberInfo[key][nameof(MonitoringInfo.Typhoon_Mode)], out var typhoon_mode);
            monitoringInfo.Typhoon_Mode = typhoon_mode;

            monitoringInfo.Manual = 0;
            monitoringInfo.Start_Flag = 1;
            monitoringInfo.Stage_Flag = 0;
            monitoringInfo.Status = "pausing";
            monitoringInfo.Sms_Name = string.Empty;
            monitoringInfo.Sms_Time = null;
            monitoringInfo.Start_Time = null;
            monitoringInfo.End_Time = null;
            monitoringInfo.Pre_Start = null;
            monitoringInfo.Pre_End = null;
            monitoringInfo.Run_End = null;
            monitoringInfo.Shell_Name = string.Empty;
            monitoringInfo.Shell_Time = null;
            monitoringInfo.Error_Message = string.Empty;
        }

        private async Task SetMonitoringInfoAsync(MonitoringInfo monitoringInfo, ModelMember info, IEnumerable<BatchConfig> batchConfig, string logFilePath)
        {
            var replace_str = new List<string>() { "\r", "\n", "\r\n", " ", "\n\r", "]", "[", ".sms", "none", ".ksh" };
            var logDatas = await _logFileService.ReadLogFileAsync(logFilePath);

            foreach (var logData in logDatas.Split("\n"))
            {
                var buffer = logData.ToString();
                //Model Member M|P DTG
                if (Regex.IsMatch(buffer, $"^{info.Model_Name} {info.Member_Name} (M|P|[0-9]|Step)"))
                {
                    //manual flag : check this run is auto or manual
                    monitoringInfo.Manual = Regex.IsMatch(buffer, "OP") ? 1 : 0;
                    monitoringInfo.Start_Flag = 1; //start_flag=1 代表尚未抓取start_time ,start_flag=0代表已抓取完成
                    monitoringInfo.Stage_Flag = 0; //stage_flag=0 代表.sms未填入
                    replace_str.ForEach(x => monitoringInfo.Run_Type = monitoringInfo.Run_Type.Replace(x, string.Empty));
                    monitoringInfo.Status = monitoringInfo.Lid == 1 ? "RUNNING" : monitoringInfo.Status;
                    monitoringInfo.Sms_Name = string.Empty;
                    monitoringInfo.Sms_Time = null;
                    monitoringInfo.Shell_Name = string.Empty;

                    continue;
                }

                //[ HH:MM:SS ] -> sms_name.sms
                //[HH:MM:SS] -> name.ksh stage_start
                var isMatchStageStart = Regex.IsMatch(buffer, "stage_start");
                if ((Regex.IsMatch(buffer, ".sms") || isMatchStageStart)
                    && Regex.IsMatch(buffer, "->"))
                {
                    monitoringInfo.Status = (Regex.IsMatch(buffer, "finish")) ? "PAUSING" : monitoringInfo.Status; //status:finish
                    monitoringInfo.Status = (Regex.IsMatch(buffer, "Cancelled")) ? "Cancelled" : monitoringInfo.Status; //status:Cancelled
                    monitoringInfo.Status = (Regex.IsMatch(buffer, "fail")) ? "FAIL" : monitoringInfo.Status; //status:fail
                    var pattern = "/\\[\\s*(\\d{2}):(\\d{2}):(\\d{2})\\s*\\](\\s+)(->|<-)+(\\s+)(\\w+)/";
                    var replacement = "${1}:$2:$3 $7";

                    buffer = Regex.Replace(buffer, pattern, replacement);
                    if (isMatchStageStart == false || monitoringInfo.Stage_Flag == 0)
                    {
                        var splitBuffer = buffer.Split(" ");
                        monitoringInfo.Sms_Time = DateTime.TryParse(splitBuffer[0], out var time)
                            ? time : monitoringInfo.Sms_Time;
                        monitoringInfo.Sms_Name = splitBuffer[1];
                    }
                    monitoringInfo.Status = monitoringInfo.Lid == 1 ? "RUNNING" : monitoringInfo.Status;
                    monitoringInfo.Stage_Flag = isMatchStageStart ? monitoringInfo.Stage_Flag : 1;
                    monitoringInfo.Shell_Name = string.Empty;

                    //=====替換掉多餘字元=====
                    replace_str.ForEach(x => monitoringInfo.Sms_Name.Replace(x, string.Empty));

                    //判斷起始時間
                    if (monitoringInfo.Start_Flag == 1)
                    {
                        monitoringInfo.Start_Time = monitoringInfo.Sms_Time;
                        monitoringInfo.Start_Flag = 0; //start_time已抓取完成
                    }

                    //predict batch start and end time
                    var start = monitoringInfo.Start_Time;
                    var sms_start_time = monitoringInfo.Sms_Time; //工作包起始時間
                    var min0 = await GetPartTimeAsync(info.Model_Name, info.Member_Name, info.Nickname, monitoringInfo.Sms_Name, monitoringInfo.Run_Type, monitoringInfo.Dtg, batchConfig); //工作包預測起始時間(前面工作時間)
                    var min1 = await GetBatchInfoByNameAsync(info.Model_Name, info.Member_Name, info.Nickname, monitoringInfo.Sms_Name, monitoringInfo.Run_Type, monitoringInfo.Dtg, nameof(BatchConfig.Time)); //工作包時間長度

                    //min2模組全部執行完成所需時間
                    var min2 = await GetTotalTimeAsync(info.Model_Name, info.Member_Name, info.Nickname, monitoringInfo.Run_Type, monitoringInfo.Dtg, batchConfig);
                    monitoringInfo.Pre_Start = start.Value.AddMinutes(min0);  //工作包預測起始時間(實際時間+前面工作時間)
                    monitoringInfo.Pre_End = sms_start_time.Value.AddMinutes(min1); //預測工作包結束時間(實際起始+工作長度)
                    monitoringInfo.End_Time = start.Value.AddMinutes(min2); //預測模組全部執行完成時間(實際起始+所有工作長度)

                    //例外處理finish 或 fail的判斷
                    if (Regex.IsMatch(buffer, "finish"))
                    {
                        monitoringInfo.Status = "PAUSING";
                    }
                    else if (Regex.IsMatch(buffer, "Cancelled"))
                    {
                        monitoringInfo.Status = "Cancelled";
                    }
                    else if (Regex.IsMatch(buffer, "fail"))
                    {
                        monitoringInfo.Status = "FAIL";
                    }

                    continue;
                }

                //[ HH:MM:SS ]
                if (Regex.IsMatch(buffer, "[\\s*(\\d{2}):(\\d{2}):(\\d{2})\\s*]"))
                {
                    var pattern = "/\\[\\s*(\\d{2}):(\\d{2}):(\\d{2})\\s*\\](\\s+)(.)+(\\s+)(\\w+)/";
                    var replacement = "${1}:$2:$3 $7";
                    buffer = Regex.Replace(pattern, replacement, buffer);
                    //Regex.IsMatch("[\s*(\d{2}):(\d{2}):(\d{2})\s*]",buffer)會有誤判
                    if (buffer.Split(" ").Length < 2) continue;

                    var splitBuffer = buffer.Split(" ");
                    monitoringInfo.Shell_Time = DateTime.TryParse(splitBuffer[0], out var time)
                        ? time : monitoringInfo.Shell_Time;
                    var shtmp = splitBuffer[1];
                    monitoringInfo.Shell_Name = Regex.IsMatch(shtmp, "/.*\\.k?sh/") ? shtmp : monitoringInfo.Shell_Name;

                    //判斷起始時間
                    if (monitoringInfo.Start_Flag == 1)
                    {
                        monitoringInfo.Start_Time = monitoringInfo.Shell_Time;
                        var start = monitoringInfo.Start_Time;

                        //min2模組全部執行完成所需時間
                        var min2 = await GetTotalTimeAsync(info.Model_Name, info.Member_Name, info.Nickname, monitoringInfo.Run_Type, monitoringInfo.Dtg, batchConfig);
                        monitoringInfo.End_Time = start.Value.AddMinutes(min2); //預測模組全部執行完成時間
                        monitoringInfo.Start_Flag = 0; //start_time已抓取完成
                    }

                    //例外處理 finish 或 fail 的判斷
                    if (Regex.IsMatch(buffer, "finish"))
                    {
                        monitoringInfo.Run_End = monitoringInfo.Shell_Time; //記錄結束時間
                        monitoringInfo.Status = "PAUSING";
                        if (monitoringInfo.Run_End == null)
                        {
                            var runEndString = Regex.Replace(buffer, "/\\[\\s*(\\d{2}):(\\d{2}):(\\d{2})\\s*\\](\\s+)(\\w+)/", "${1}:$2:$3");
                            monitoringInfo.Run_End = DateTime.TryParse(runEndString, out var runEnd)
                                ? runEnd : monitoringInfo.Run_End;
                        }
                    }
                    else if (Regex.IsMatch(buffer, "Cancelled"))
                    {
                        monitoringInfo.Run_End = monitoringInfo.Shell_Time; //記錄結束時間
                        monitoringInfo.Status = "Cancelled";
                        if (monitoringInfo.Run_End == null)
                        {
                            var runEndString = Regex.Replace(buffer, "/\\[\\s*(\\d{2}):(\\d{2}):(\\d{2})\\s*\\](\\s+)(\\w+)/", "${1}:$2:$3");
                            monitoringInfo.Run_End = DateTime.TryParse(runEndString, out var runEnd)
                                ? runEnd : monitoringInfo.Run_End;
                        }

                        var now_time = DateTime.Now.TWNow();
                        monitoringInfo.Error_Message = $"[{now_time}]{info.Model_Name}_{info.Member_Name}_{info.Nickname}(C) {monitoringInfo.Status}, run_end:{monitoringInfo.Run_End}\n";
                    }
                    else if (Regex.IsMatch(buffer, "fail"))
                    {
                        monitoringInfo.Run_End = (monitoringInfo.Status != "running") ? monitoringInfo.Shell_Time : null; //記錄結束時間
                        monitoringInfo.Status = "FAIL";
                        if (monitoringInfo.Run_End == null)
                        {
                            var runEndString = Regex.Replace(buffer, "/\\[\\s*(\\d{2}):(\\d{2}):(\\d{2})\\s*\\](\\s+)(\\w+)/", "${1}:$2:$3");
                            monitoringInfo.Run_End = DateTime.TryParse(runEndString, out var runEnd)
                                ? runEnd : monitoringInfo.Run_End;
                        }
                        var now_time = DateTime.Now.TWNow();
                        monitoringInfo.Error_Message = $"[{now_time}]{info.Model_Name}_{info.Member_Name}_{info.Nickname}(C) {monitoringInfo.Status}, run_end:{monitoringInfo.Run_End}\n";
                    }

                    continue;
                }
            }
        }

        /// <summary>
        /// 模組全部執行完成所需時間
        /// </summary>
        /// <param name="model_Name"></param>
        /// <param name="member_Name"></param>
        /// <param name="nickname"></param>
        /// <param name="run_type"></param>
        /// <param name="dtg"></param>
        /// <param name="batchConfig"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<int> GetTotalTimeAsync(string model_Name, string member_Name, string nickname, string run_type, string dtg, IEnumerable<BatchConfig> batchConfig)
        {
            var count = 0;     //計算總時間

            //若DB完全沒有工作包的資訊 回傳-1
            if (batchConfig == null) return -1;

            for (var i = 0; i < batchConfig.Count(); i++)
            {
                var batch = batchConfig.ElementAt(i);

                var position = batch.Position; //batch順序
                var number = batchConfig.Count(x => x.Position == position); //計算同層batch個數

                //表示batch同層無重複
                if (number < 2)
                {
                    count += batch.Time;
                    continue;
                }

                //表示batch同層有重複
                count += await GetBatchInfoByPositionAsync(model_Name, member_Name, nickname, position, run_type, dtg, nameof(BatchConfig.Time));
                i += number - 1;  //position由sql指令排序過
            }
            return count;
        }

        /// <summary>
        /// 工作包預測起始時間(前面工作時間長度)
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="memberName"></param>
        /// <param name="nickname"></param>
        /// <param name="smsName"></param>
        /// <param name="runType"></param>
        /// <param name="dtg"></param>
        /// <param name="batchConfig"></param>
        /// <returns></returns>
        private async Task<int> GetPartTimeAsync(string modelName, string memberName, string nickname, string smsName, string runType, string dtg, IEnumerable<BatchConfig> batchConfig)
        {
            var key = modelName + memberName + nickname;
            var count = 0;     //計算總時間
            var tmpPosition = await GetBatchInfoByNameAsync(modelName, memberName, nickname, smsName, runType, dtg, nameof(BatchConfig.Position)); //取出工作包的次序

            if (tmpPosition > 0)
            {  //工作包次序 > 0 表示有前面的工作
                for (var i = 0; i < batchConfig.Count(); i++)  //所有的batch資訊
                {
                    var batch = batchConfig.ElementAt(i);
                    var position = batch.Position; //batch順序
                    var number = batchConfig.Count(x => x.Position == position); //計算同層batch個數

                    if (position >= tmpPosition) return count;
                    if (number < 2) //表示batch同層無重複
                    {
                        count += batch.Time;
                        continue;
                    }

                    count += await GetBatchInfoByPositionAsync(modelName, memberName, nickname, position, runType, dtg, nameof(BatchConfig.Time));
                    i += number - 1;  //position由sql指令排序過
                }
            }

            return count;
        }

        /// <summary>
        /// 單一工作包時間長度(由次序查詢)依model, member, position, type, dtg, 指定查詢column 值
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="memberName"></param>
        /// <param name="nickname"></param>
        /// <param name="position"></param>
        /// <param name="runType"></param>
        /// <param name="dtg"></param>
        /// <param name="getColumnName"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private async Task<int> GetBatchInfoByPositionAsync(string modelName, string memberName, string nickname, int position, string runType, string dtg, string getColumnName)
        {
            var batchItems = await GetBatchByPositionAsync(modelName, memberName, nickname, position, runType);
            var firstBatchItem = batchItems.FirstOrDefault();

            if (batchItems.Count() == 0)  // 若 同步驟 同執行型態無
            {
                batchItems = await GetBatchByPositionAsync(modelName, memberName, nickname, position, string.Empty);
                firstBatchItem = batchItems.FirstOrDefault();
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);
            }

            if (batchItems.Count() == 1)  // 若 同步驟 同執行型態僅有一個
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);

            //若 同步驟 同執行型態有兩個以上
            dtg = dtg.Length >= 8 ? dtg.Substring(6, 2) : string.Empty;
            batchItems = await GetBatchByPositionAsync(modelName, memberName, nickname, position, runType, dtg);
            firstBatchItem = batchItems.FirstOrDefault();
            if (batchItems.Count() >= 1)  // 若 同步驟 同執行型態 同dtg值僅有一個以上(取第一個)
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);

            batchItems = await GetBatchByPositionAsync(modelName, memberName, nickname, position, runType, string.Empty);
            firstBatchItem = batchItems.FirstOrDefault();
            if (batchItems.Count() >= 1)
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);

            batchItems = await GetBatchByPositionAsync(modelName, memberName, nickname, position, string.Empty);
            firstBatchItem = batchItems.FirstOrDefault();
            return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);
        }

        /// <summary>
        /// 單一工作包時間長度(由名稱查詢)
        /// </summary>
        /// <param name="modelName"></param>
        /// <param name="memberName"></param>
        /// <param name="nickname"></param>
        /// <param name="smsName"></param>
        /// <param name="runType"></param>
        /// <param name="dtg"></param>
        /// <param name="getColumnName"></param>
        /// <returns></returns>
        private async Task<int> GetBatchInfoByNameAsync(string modelName, string memberName, string nickname, string smsName, string runType, string dtg, string getColumnName)
        {
            var batchItems = await GetBatchByNameAsync(modelName, memberName, nickname, smsName); //先查詢batch名稱
            if (batchItems.Count() == 0) return -1;

            var firstBatchItem = batchItems.FirstOrDefault();
            if (batchItems.Count() == 1)  //若同名稱有一個
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);

            //若同名稱有兩個以上
            batchItems = await GetBatchByNameAsync(modelName, memberName, nickname, smsName, runType);
            firstBatchItem = batchItems.FirstOrDefault();
            if (batchItems.Count() == 0)
            {
                batchItems = await GetBatchByNameAsync(modelName, memberName, nickname, smsName, string.Empty);
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);
            }

            if (batchItems.Count() == 1)
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);

            //若同名稱、同型態有兩個以上
            dtg = dtg.Length >= 8 ? dtg.Substring(6, 2) : string.Empty;
            batchItems = await GetBatchByNameAsync(modelName, memberName, nickname, smsName, runType, dtg);
            firstBatchItem = batchItems.FirstOrDefault();
            if (batchItems.Count() >= 1)  //若同名稱、同型態、同dtg值有一個以上
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);

            batchItems = await GetBatchByNameAsync(modelName, memberName, nickname, smsName, runType, string.Empty);
            firstBatchItem = batchItems.FirstOrDefault();
            if (batchItems.Count() >= 1)
                return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);

            batchItems = await GetBatchByNameAsync(modelName, memberName, nickname, smsName, string.Empty);
            firstBatchItem = batchItems.FirstOrDefault();
            return (int)firstBatchItem.GetType().GetProperties().FirstOrDefault(x => x.Name == getColumnName).GetValue(firstBatchItem);
        }

        private async Task<IEnumerable<BatchView>> GetBatchByNameAsync(string modelName, string memberName, string nickname, string smsName, string runType = null, string dtg = null)
        {
            var sql = $@"SELECT * FROM batch_view
                         WHERE model = @Model
                         AND member = @Member
                         AND nickname = @Nickname
                         AND batch = @SmsName
                         {(runType != null ? "AND type = @RunType" : string.Empty)}
                         {(dtg != null ? "AND dtg = @Dtg" : string.Empty)}
                         ORDER BY position ASC, type ASC, dtg ASC;";

            var param = new
            {
                Model = modelName,
                MemberName = memberName,
                NickName = nickname,
                SmsName = smsName,
                RunType = runType,
                Dtg = dtg,
            };
            return await _dataBaseNcsUiService.QueryAsync<BatchView>(sql, param);
        }

        private async Task<IEnumerable<BatchView>> GetBatchByPositionAsync(string modelName, string memberName, string nickname, int position, string runType, string dtg = null)
        {
            var sql = $@"SELECT * FROM batch_view
                         WHERE model = @Model
                         AND member = @Member
                         AND nickname = @Nickname
                         AND position = @Position
                         and type = @RunType
                         {(dtg != null ? "AND dtg = @Dtg" : string.Empty)}";
            var param = new
            {
                Model = modelName,
                MemberName = memberName,
                NickName = nickname,
                Position = position,
                RunType = runType,
                Dtg = dtg,
            };
            return await _dataBaseNcsUiService.QueryAsync<BatchView>(sql, param);
        }

        private async Task<IEnumerable<BatchConfig>> GetBatchConfigAsync()
        {
            var sql = @"SELECT concat(`model`.`model_name`,`member`.`member_name`,`member`.`nickname`) AS model_member_nick,
                            `batch`.`batch_name` AS `batch`,
                            `batch`.`batch_position` AS `position`,
                            `batch`.`batch_type` AS `type`,
                            `batch`.`batch_dtg` AS `dtg`,
                            `batch`.`batch_time` AS `time`
                        FROM
                            ((`model` join `member`) join `batch`)
                        WHERE
                            ((`model`.`model_id` = `member`.`model_id`)
                            AND(`member`.`member_id` = `batch`.`member_id`))
                        ORDER BY
                            model_member_nick,
                            `batch`.`batch_position`;";
            var result = (await _dataBaseNcsUiService.QueryAsync<BatchConfig>(sql)).ToList();
            result.ForEach(x => x.Batch = x.Batch.Trim());

            return result;
        }

        private async Task<IEnumerable<ModelMember>> GetModelConfigAsync()
        {
            var sql = @"SELECT *
                        FROM(`member`, `model`)
                        WHERE
                            `model`.`model_id` = member.model_id
                        ORDER BY
                            `model_position` asc,
                            `member_position` asc
                        ";
            var result = await _dataBaseNcsUiService.QueryAsync<ModelMember>(sql);
            return result;
        }

        private Dictionary<string, Dictionary<string, string>> ParseString(string str, bool processSections)
        {
            var lines = str.Split("\n");
            var result = new Dictionary<string, Dictionary<string, string>>();
            var inSect = string.Empty;
            foreach (var line in lines)
            {
                var item = line.Trim();
                if (item == null || item.StartsWith("#") || item.StartsWith(";"))  //註解跳過
                    continue;

                if (item.StartsWith("[") && item.IndexOf("]") > -1)  //元素開頭 ex:[/ncs/npcapln/NFS/M03]
                {
                    inSect = item.Substring(1, item.Length - item.IndexOf("]") - 1);
                    continue;
                }
                if (item.IndexOf("=") == -1)  //(We don't use "=== false" because value 0 is not valid as well)
                    continue;

                var spiltItem = item.Split("=", 2);
                if (processSections && inSect.Any())  //ProcessSections=true  多元素(二維陣列)
                    result.Add(inSect, new Dictionary<string, string> { { spiltItem[0].Trim(), spiltItem[1].Trim() } });
                //else  //ProcessSections=false 單一元素(一維陣列)
                //    return[trim(tmp[0])] = ltrim(tmp[1]);
            }
            return result;
        }

        #endregion Private Method
    }
}