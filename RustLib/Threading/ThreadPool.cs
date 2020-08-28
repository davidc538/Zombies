using System;
using System.Collections.Generic;
using System.Threading;
using System.Collections.Concurrent;

namespace RustLib.Threading
{
	public class ThreadPool
	{
		private class Job
		{
			private Action job;
			private Action callback;
			private Exception exception;

			public Job(Action job, Action callback)
			{
				this.job = job;
				this.callback = callback;
			}

			public void DoJob()
			{
				try { job?.Invoke(); }
				catch (Exception e) { exception = e; }
			}

			public void DoCallback(Action<object> output = null)
			{
				if (exception != null)
					output?.Invoke(exception);

				try { callback?.Invoke(); }
				catch (Exception e) { output?.Invoke(e); }
			}
		}

		private ConcurrentQueue<Job> unfinished_job_queue = new ConcurrentQueue<Job>();
		private ConcurrentQueue<Job> finished_job_queue = new ConcurrentQueue<Job>();
		private List<Thread> threads = new List<Thread>();

		private const int default_thread_count = 32;
		private readonly int thread_count;
		private bool is_shutdown = false;

		public ThreadPool(int thread_count = default_thread_count)
		{
			this.thread_count = thread_count;

			Initialize();
		}

		public void Shutdown() => is_shutdown = true;

		public void EnqueueJob(Action job, Action callback) => unfinished_job_queue.Enqueue(new Job(job, callback));

		public void ProcessCallbacks(Action<object> output = null)
		{
			while (finished_job_queue.TryDequeue(out Job job))
				job.DoCallback(output);
		}

		private void RunThread()
		{
			while (!is_shutdown)
			{
				while (unfinished_job_queue.TryDequeue(out Job job))
				{
					job.DoJob();
					finished_job_queue.Enqueue(job);
				}

				Thread.Sleep(1);
			}
		}

		private void Initialize()
		{
			for (int i = 0; i < thread_count; i++)
			{
				Thread thread = new Thread(RunThread);
				thread.IsBackground = true;
				thread.Start();
				threads.Add(thread);
			}
		}
	}
}