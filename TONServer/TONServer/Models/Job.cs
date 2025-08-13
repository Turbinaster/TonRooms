using Libs;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TONServer
{
    [DisallowConcurrentExecution]
    public class Job : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            try
            {
                await Task.Delay(5000);
                await _Hub.Send("send", "job");
            }
            catch (Exception ex) { Helper.Log(ex); }
        }
    }
}
