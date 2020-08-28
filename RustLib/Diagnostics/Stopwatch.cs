using System;

namespace RustLib.Diagnostics
{
	public class Stopwatch : IDisposable
	{
		private System.Diagnostics.Stopwatch sw;
		private string name;
		private Action<object, long, long> output;

		public Stopwatch(string name, Action<object, long, long> output)
		{
			this.name = name;
			this.output = output;

			Start();
		}

		public Stopwatch(string name, PerformanceReport debug_file)
		{
			this.name = name;
			this.output = debug_file.Report;

			Start();
		}

		private void Start()
		{
			sw = new System.Diagnostics.Stopwatch();
			sw.Start();
		}

		public void Dispose()
		{
			sw.Stop();
			output?.Invoke($"SW: {name}", sw.ElapsedMilliseconds, sw.ElapsedTicks);
		}
	}
}
