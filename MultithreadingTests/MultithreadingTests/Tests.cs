using System;
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
            int sharedResource = 0;
            int testResource = 0;

            ThreadStart threadFct = () =>
            {
                int partialTestValue = 0;

                var random = new Random();
                for (int idx = 0; idx < 100000; idx++)
                {
                    int value = random.Next(); //generate random value

                    //access the shared resource without locking
                    sharedResource += value;

                    //also save the value in a local variable, to measure against the sharedResource
                    partialTestValue += value;
                }

                //add together all values, to build up the expected result of the operation
                Interlocked.Add(ref testResource, partialTestValue);
            };

            //start all threads
            var threads = new List<Thread>();
            for(int idx = 0; idx < 20; idx ++)
            {
                var thread = new Thread(threadFct);
                thread.Start();
                threads.Add(thread);
            }


            //wait for the threads to complete
            threads.ForEach(t => t.Join());

            //sharedResource should 'theoretically' be identical to testResource, because they are 
            //the result of the same additions
            if (sharedResource != testResource)
                Console.WriteLine("Race condition detected");
            else
                Console.WriteLine("No race happened");

        }
    }
}
