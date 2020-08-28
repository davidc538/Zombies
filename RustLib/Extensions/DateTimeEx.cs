using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RustLib.Extensions
{
	public static class DateTimeEx
	{
		public static string ToFileName(this DateTime datetime)
		{
			string y = datetime.Year.ToString();
			string M = datetime.Month.ToString().PadLeft(2, '0');
			string d = datetime.Day.ToString().PadLeft(2, '0');
			string h = datetime.Hour.ToString().PadLeft(2, '0');
			string m = datetime.Minute.ToString().PadLeft(2, '0');
			string s = datetime.Second.ToString().PadLeft(2, '0');
			string ret = $"y{y}M{M}d{d}h{h}m{m}s{s}";
			return ret;
		}
	}
}
