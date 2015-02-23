using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultithreadingTests
{
    public class Tests
    {
        public static void RaceCondition()
        {
            //will access sharedResource by doing additions from unsynchronized context
            long sharedResource = 0;

            var testPartialResults = new long[20];
            ParameterizedThreadStart threadFct = (object param) =>
            {
                long partialTestValue = 0;

                var random = new Random();
                for (int idx = 0; idx < 100000; idx++)
                {
                    int value = random.Next(); //generate random value

                    //access the shared resource without locking
                    sharedResource += value;

                    //also save the value in a local variable, to measure against the sharedResource
                    partialTestValue += value;
                }

                //save the partialTestResource
                testPartialResults[(int)param] = partialTestValue;
            };

            //start all threads
            var threads = Enumerable.Range(0, 20).Select(idx =>
            {
                var thr = new Thread(threadFct);
                thr.Start(idx);
                return thr;
            }).ToList();


            //wait for the threads to complete
            threads.ForEach(t => t.Join());

            long testResource = testPartialResults.Sum();

            //sharedResource should 'theoretically' be identical to testResource,
            //because they are the result of the same additions
            //truth is that in most cases they will not be the same because of the threads overwriting each other's operation
            Console.WriteLine("Shared resource value: {0}; Test value {1}", sharedResource, testResource);
            if (sharedResource != testResource)
                Console.WriteLine("Race condition detected");
            else
                Console.WriteLine("You were lucky - no race happened");
        }
    
    
        public static void CriticalSection()
        {
            int nLoops = 1000000, nThreads = 40;

            var lockObject = new object();
            int sharedResource = 0;

            ThreadStart threadFunction = () =>
            {
                for (int idx = nLoops - 1; idx >= 0; idx--)
                {
                    lock (lockObject)
                    {
                        sharedResource++;
                    }
                }
            };

            var threads = Enumerable.Range(0, nThreads).Select(n => new Thread(threadFunction)).ToList();

            //start all 10 threads
            threads.ForEach(t => t.Start());

            //wait for all threads to exit
            threads.ForEach(t => t.Join());

            Console.WriteLine("Shared resource value: {0}; expected {1}", sharedResource, nLoops * nThreads);
        }


        public static void CriticalSectionPerformanceNoLocks()
        {
            CSPerformanceTest(() => {}, () => {});
        }

        public static void CriticalSectionPerformance()
        {
            var lockObject = new object();
            CSPerformanceTest(() => { Monitor.Enter(lockObject); }, () => { Monitor.Exit(lockObject); });
        }

        public static void InterlockedPerformance()
        {

        }

        private static void CSPerformanceTest(Action doLock, Action releaseLock)
        {
            //This demo is fairly similar to the pervious one
            //the only difference is the ability to run without locking
            //to prove the performance penalty that locks bring
            int nLoops = 1000000, nThreads = 40;

            int sharedResource = 0;

            ThreadStart threadFunction = () =>
            {
                for (int idx = nLoops - 1; idx >= 0; idx--)
                {
                    doLock();
                    sharedResource++;
                    releaseLock();
                }
            };

            var threads = Enumerable.Range(0, nThreads).Select(n => new Thread(threadFunction)).ToList();

            //start all 10 threads
            threads.ForEach(t => t.Start());

            //wait for all threads to exit
            threads.ForEach(t => t.Join());

            Console.WriteLine("Shared resource value: {0}; expected {1}", sharedResource, nLoops * nThreads);
        }


        private class Resource
        {
            public int Number { get; set; }
            public long ItsSquare { get; set; }
            public string DisplayString { get; set; }

            public void ReloadValue(Random random)
            {
                Number = random.Next();
                ItsSquare = (long)Number * Number;
                DisplayString = string.Format("Number: {0}; It's square: {1}", Number, ItsSquare);
            }
        }

        public static void ReadWriteLocks()
        {
            var rwLock = new ReaderWriterLock();
            var cachedResource = new Resource();
            bool writeThreadRunning = true;
            var random = new Random();

            //set the initial values of the cache
            cachedResource.ReloadValue(random);

            ThreadStart writeThread = () =>
                {
                    
                    for (int idx = 0; idx < 10; idx++ )
                    {
                        rwLock.AcquireWriterLock(0);

                        //let's say we need to refresh the cache, but we're constrained to using the same object we had before
                        //reloading object like this is FAR FROM BEST PRACTICE, but I'm using it to demo the RWlocks
                        cachedResource.ReloadValue(random);

                        rwLock.ReleaseLock();
                        Console.Write("w");
                        Thread.Sleep(1000);
                    }
                    writeThreadRunning = false;
                };

            ThreadStart readerThread = () =>
            {
                int lastNumber = -1;
                int accumulation = 0;

                while(writeThreadRunning)
                {
                    rwLock.AcquireReaderLock(0);
                    //consume the value - do some assertions
                    if ((long)cachedResource.Number * cachedResource.Number != cachedResource.ItsSquare ||
                        cachedResource.DisplayString != string.Format("Number: {0}; It's square: {1}", cachedResource.Number, cachedResource.ItsSquare))
                        throw new InvalidOperationException("Assertions on the shared resource failed");

                    //do some action on the cachedResource
                    if(lastNumber != cachedResource.Number)
                    {
                        lastNumber = cachedResource.Number;
                        accumulation += cachedResource.Number;
                    }

                    rwLock.ReleaseReaderLock();
                    Thread.Sleep(50);
                }
            };

            //start all readers
            var readers = Enumerable.Range(0, 20).Select(n => new Thread(readerThread)).ToList();
            readers.ForEach(r => r.Start());

            //run writer on main thread
            writeThread();

            //wait for readers to exit
            readers.ForEach(r => r.Join());
        }

        private class ConsummerContext
        {
            public long Result { get; set; }
        }

        public static void ConcurrentQueueExample()
        {
            int nProducers = 2;
            int nConsummers = 7;
            //you can use the same instance of queue to do message based communication between threads
            var concurrentQueue = new ConcurrentQueue<int>();
            long testValue = 0;

            var consummerCtx = Enumerable.Range(0, nConsummers).Select(n => new ConsummerContext { Result = 0}).ToArray();

            ThreadStart producerThread = () =>
                {
                    var random = new Random();
                    for( int idx = 0; idx < 1000000; idx ++)
                    {
                        var generatedNumber = random.Next();
                        concurrentQueue.Enqueue(generatedNumber);
                        Interlocked.Add(ref testValue, (long) generatedNumber);
                    }
                };

            bool consummersRunning = true;
            ParameterizedThreadStart consummerThread = param =>
                {
                    var myContext = (ConsummerContext)param;

                    while (consummersRunning)
                    {
                        int number;
                        while (concurrentQueue.TryDequeue(out number))
                        {
                            myContext.Result += number;
                        }

                        //nothing for me to do, so I'll give up control to other threads
                        Thread.Yield();
                    }
                };

            //start consummers first - otherwise, there'll be loads of messages accumulated in memory
            var consummers = Enumerable.Range(0, nConsummers).Select(idx =>
            {
                var t = new Thread(consummerThread);
                t.Start(consummerCtx[idx]);
                return t;
            }).ToList();

            //start producers
            var producers = Enumerable.Range(0, nProducers).Select(
                idx =>
                {
                    var t = new Thread(producerThread);
                    t.Start();
                    return t;
                }).ToList();

            //wait for producers to finish
            producers.ForEach(p => p.Join());

            SpinWait.SpinUntil(() => concurrentQueue.IsEmpty);

            consummersRunning = false;
            consummers.ForEach(c => c.Join());

            //check validity
            var computedSum = consummerCtx.Sum(c => c.Result);
            Console.WriteLine("Computed value: {0}; test value: {1}", computedSum, testValue);

            if (consummerCtx.Sum(c => c.Result) != testValue)
                throw new InvalidOperationException("Computed sum does not match test value");
        }



        public static void ManualResetEventExample()
        {
            var evt = new ManualResetEvent(false);


            ThreadStart threadFunc = () =>
            {
                Console.WriteLine("Thr{0}: blocking on event", Thread.CurrentThread.ManagedThreadId);
                evt.WaitOne();
                Console.WriteLine("Thr{0} got unblocked", Thread.CurrentThread.ManagedThreadId);
            };

            var threads = Enumerable.Range(0, 10).Select(idx => new Thread(threadFunc)).ToList();
            threads.ForEach(t => t.Start());
            Thread.Sleep(1000);

            Console.WriteLine("Thr{0}: notifying event", Thread.CurrentThread.ManagedThreadId);
            evt.Set();

            threads.ForEach(t => t.Join());
        }

        public static void AutoResetEventExample()
        {
            var evt = new AutoResetEvent(false);

            ThreadStart threadFunc = () =>
            {
                Console.WriteLine("Thr{0}: blocking on event", Thread.CurrentThread.ManagedThreadId);
                evt.WaitOne();
                Console.WriteLine("Thr{0} got unblocked", Thread.CurrentThread.ManagedThreadId);
            };

            var threads = Enumerable.Range(0, 10).Select(idx => new Thread(threadFunc)).ToList();
            threads.ForEach(t => t.Start());
            Thread.Sleep(1000);

            for (int idx = 0; idx < 10; idx++)
            {
                Console.WriteLine("Thr{0}: notifying event", Thread.CurrentThread.ManagedThreadId);
                evt.Set();
                Thread.Sleep(1000);
            }

            threads.ForEach(t => t.Join());
        }


        private class ResourceImmutable
        {
            public int Number { get; private set; }
            public long ItsSquare { get; private set; }
            public string DisplayString { get; private set; }

            public ResourceImmutable(Random random)
            {
                Number = random.Next();
                ItsSquare = (long)Number * Number;
                DisplayString = string.Format("Number: {0}; It's square: {1}", Number, ItsSquare);
            }
        }

        public static void ReadWriteLocksExampleRewrittenWithImmutable()
        {
         
            bool writeThreadRunning = true;
            var random = new Random();

            //set the initial values of the cache
            var cachedResource = new ResourceImmutable(random);

            ThreadStart writeThread = () =>
            {

                for (int idx = 0; idx < 10; idx++)
                {
                    //let's say we need to refresh the cache
                    //forget about the old object, just create a new one with the new values
                    cachedResource = new ResourceImmutable(random);
                    
                    Console.Write("w");
                    Thread.Sleep(1000);
                }
                writeThreadRunning = false;
            };

            ThreadStart readerThread = () =>
            {
                int lastNumber = -1;
                int accumulation = 0;

                while (writeThreadRunning)
                {
                    //consume the value - do some assertions
                    
                    //make a local copy of the object in question, to prevent it from being changed once we're starting to work with it
                    var localCopy = cachedResource;

                    if ((long)localCopy.Number * localCopy.Number != localCopy.ItsSquare ||
                        localCopy.DisplayString != string.Format("Number: {0}; It's square: {1}", localCopy.Number, localCopy.ItsSquare))
                        throw new InvalidOperationException("Assertions on the shared resource failed");

                    //do some action on the cachedResource
                    if (lastNumber != cachedResource.Number)
                    {
                        lastNumber = cachedResource.Number;
                        accumulation += cachedResource.Number;
                    }

                    Thread.Sleep(50);
                }
            };

            //start all readers
            var readers = Enumerable.Range(0, 20).Select(n => new Thread(readerThread)).ToList();
            readers.ForEach(r => r.Start());

            //run writer on main thread
            writeThread();

            //wait for readers to exit
            readers.ForEach(r => r.Join());
        }
    }
}
