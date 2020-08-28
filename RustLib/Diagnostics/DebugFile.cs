using RustLib.Extensions;
using System;
using System.Collections.Generic;
using System.IO;

namespace RustLib.Diagnostics
{
	public class PerformanceReport : IDisposable
	{
		private string filename;
		private Dictionary<string, long> message_ms = new Dictionary<string, long>();
		private Dictionary<string, long> message_ticks = new Dictionary<string, long>();
		private Dictionary<string, long> entries = new Dictionary<string, long>();
		private Queue<string> queue = new Queue<string>();

		public PerformanceReport(string filename)
		{
			this.filename = filename;
		}

		public static PerformanceReport CurrentTime()
		{
			string filename = DateTime.Now.ToFileName();
			PerformanceReport ret = new PerformanceReport(filename);
			return ret;
		}

		public void Report(string info, long ms, long ticks)
		{
			if (!message_ms.ContainsKey(info))
				message_ms[info] = 0;

			message_ms[info] += ms;

			if (!message_ticks.ContainsKey(info))
				message_ticks[info] = 0;

			message_ticks[info] += ticks;

			if (!entries.ContainsKey(info))
				entries[info] = 0;

			entries[info]++;

			queue.Enqueue($"{ms.ToString().PadLeft(5)} ms {ticks.ToString().PadLeft(10)} ticks {info}");
		}

		public void Report(object info, long ms, long ticks)
		{
			Report(info.ToString(), ms, ticks);
		}

		public void Dispose()
		{
			List<string> messages = new List<string>(message_ms.Keys);

			messages.Sort();

			using (StreamWriter sw = new StreamWriter("logs\\perf\\" + filename + ".txt"))
			{
				sw.WriteLine($"{"total".PadLeft(5)} ms {"total".PadLeft(10)} ticks {"average".PadLeft(10)} ticks");

				foreach (string msg in messages)
				{
					long ms = message_ms[msg];
					long ticks = message_ticks[msg];
					long entry_count = entries[msg];
					long average = ticks / entry_count;

					sw.WriteLine($"{ms.ToString().PadLeft(5)} ms {ticks.ToString().PadLeft(10)} ticks {average.ToString().PadLeft(10)} {msg}");
				}

				while (queue.Count > 0)
					sw.WriteLine(queue.Dequeue());
			}
		}
	}
}
