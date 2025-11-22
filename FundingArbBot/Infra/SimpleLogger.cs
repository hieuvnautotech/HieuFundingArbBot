using System;
using HieuFundingArbBot.Infra;
namespace HieuFundingArbBot.Infra
{
    public class SimpleLogger
    {
        private readonly object _lock = new object();

        public void Info(string msg)  => Log("INFO", msg);
        public void Debug(string msg) => Log("DEBUG", msg);
        public void Warn(string msg)  => Log("WARN", msg);
        public void Error(string msg) => Log("ERROR", msg);

        private void Log(string level, string msg)
        {
            lock (_lock)
            {
                Console.WriteLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {msg}");
            }
        }
    }
}
