using System;
using System.Threading;

namespace CKKS_Agent_System.Helper
{
    public static class IDGeneration
    {
        // 起始时间戳 (2026-11-01 00:00:00 UTC)
        // 基准时间，项目上线后不再进行更改
        private const long Twepoch = 1793491200000L;

        // 机器 ID 所占位数 (5位)
        private const int WorkerIdBits = 5;
        // 数据中心 ID 所占位数 (5位)
        private const int DatacenterIdBits = 5;
        // 毫秒内自增序列所占位数 (12位)
        private const int SequenceBits = 12;

        // 计算最大值：31
        private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);
        private const long MaxDatacenterId = -1L ^ (-1L << DatacenterIdBits);

        // 位移计算
        private const int WorkerIdShift = SequenceBits;                               // 12
        private const int DatacenterIdShift = SequenceBits + WorkerIdBits;            // 17
        private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DatacenterIdBits; // 22
        private const long SequenceMask = -1L ^ (-1L << SequenceBits);                // 4095

        // 默认机器 ID 和数据中心 ID
        private const long WorkerId = 1L;
        private const long DatacenterId = 1L;

        // 状态变量
        private static long _sequence = 0L;
        private static long _lastTimestamp = -1L;

        // 线程锁，保证高并发下的线程安全
        private static readonly object _lock = new object();

        // 生成一个雪花ID (返回long类型)
        public static long NextId()
        {
            lock (_lock)
            {
                long timestamp = GetCurrentTimestamp();

                // 时钟回拨处理
                if (timestamp < _lastTimestamp)
                {
                    long offset = _lastTimestamp - timestamp;
                    
                    // 如果回拨时间很短（比如5毫秒以内），就等待一下
                    if (offset <= 5)
                    {
                        Thread.Sleep((int)offset);
                        timestamp = GetCurrentTimestamp();
                    }
                    else
                    {
                        // 回拨时间太长，抛出异常，避免生成重复ID
                        throw new InvalidOperationException($"系统时钟回拨异常。拒绝生成ID，请检查服务器时间。回拨时间：{offset} 毫秒");
                    }
                }

                // 如果是同一毫秒内生成的ID
                if (_lastTimestamp == timestamp)
                {
                    _sequence = (_sequence + 1) & SequenceMask;
                    
                    // 同毫秒内序列号用尽（超过4095），阻塞等待到下一毫秒
                    if (_sequence == 0)
                    {
                        timestamp = WaitNextMillis(_lastTimestamp);
                    }
                }
                else
                {
                    // 不同毫秒，序列号重置为 0
                    _sequence = 0L;
                }

                _lastTimestamp = timestamp;

                // 拼接64位ID并返回
                return ((timestamp - Twepoch) << TimestampLeftShift)
                       | (DatacenterId << DatacenterIdShift)
                       | (WorkerId << WorkerIdShift)
                       | _sequence;
            }
        }

        // 获取当前时间戳（毫秒）
        private static long GetCurrentTimestamp()
        {
            return (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }

        // 阻塞等待到下一毫秒
        private static long WaitNextMillis(long lastTimestamp)
        {
            long timestamp = GetCurrentTimestamp();
            while (timestamp <= lastTimestamp)
            {
                timestamp = GetCurrentTimestamp();
                Thread.SpinWait(1); // 稍微让出CPU时间片，避免死循环占满CPU
            }
            return timestamp;
        }
    }
}