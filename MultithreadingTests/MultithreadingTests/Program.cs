using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MultithreadingTests
{
    class Program
    {
        static void Main(string[] args)
        {
            //Tests.RaceCondition();

            //Tests.CriticalSection();

            //TimeMethod(() => Tests.CriticalSectionPerformanceNoLocks());
            //TimeMethod(() => Tests.CriticalSectionPerformance());


            //Tests.ReadWriteLocks();

            //Tests.ConcurrentQueueExample();
            //Tests.ManualResetEventExample();
            //Tests.AutoResetEventExample();

            //Tests.ReadWriteLocksExampleRewrittenWithImmutable();
        }


        static void TimeMethod(Action action)
        {
            var stopWatch = new Stopwatch();
            stopWatch.Start();

            action();

            stopWatch.Stop();

            Console.WriteLine("Duration: {0} milliseconds", stopWatch.Elapsed.TotalMilliseconds.ToString(".00"));

        }
    }
}
