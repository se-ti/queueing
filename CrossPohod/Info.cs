using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml.Serialization.Advanced;

namespace CrossPohod
{
	public class BaseInfo
	{
		public TimeSpan Time = TimeSpan.Zero;
		public TimeSpan Wait = TimeSpan.Zero;
		public bool Reject = false;

		public BaseInfo(BaseInfo bi)
		{
			Time = bi.Time;
			Wait = bi.Wait;
			Reject = bi.Reject;
		}
		public BaseInfo(TimeSpan time, TimeSpan wait, bool reject)
		{
			Time = time;
			Wait = wait;
			Reject = reject;
		}
	}

	public class TeamPhaseInfo : BaseInfo
	{
		public Phase Phase;
		public TeamPhaseInfo(Phase phase, BaseInfo bi)
			: base(bi)
		{
			Phase = phase;
		}
	}

	public class PhaseTeamInfo : BaseInfo
	{
		public Team Team;
		public DateTime When;
		public PhaseTeamInfo(Team team, BaseInfo bi)
			: base(bi)
		{
			Team = team;
			When = DateTime.MinValue;
		}
	}

	public class PhaseInfo
	{
		public int MaxLoad;
		public DateTime Start;
		public DateTime End;

		public PhaseInfo()
		{ }

		public PhaseInfo(int maxLoad, DateTime start, DateTime end)
		{
			MaxLoad = maxLoad;
			Start = start;
			End = end;
		}
	}

	public class TimeStat
	{
		public TimeSpan Min = TimeSpan.MaxValue;
		public TimeSpan Max = TimeSpan.MinValue;
		public TimeSpan Total = TimeSpan.Zero;
		public int Num 	{get; protected set;}

		protected  List<TimeSpan> all = new List<TimeSpan>();

		public void Add(TimeSpan span)
		{
			if (span < Min)
				Min = span;
			if (span > Max)
				Max = span;

			Total += span;
			Num++;

			all.Add(span);
		}

		public TimeSpan Mean 
		{
			get { return new TimeSpan(0, 0, 0, Num != 0 ? (int)(Total.TotalSeconds / Num) : 0); }
		}

		public static string Header()
		{
			return "min\tm95\tM95\tMax\tсредн";
		}

		public String Print(double level)
		{
			if (Num == 0)
				return "0\t0\t0\t0\t0";

			Stat<TimeSpan, long> stat = new Stat<TimeSpan, long>(all, ts => ts.Ticks);
			return string.Format("{0}\t{1}\t{2}\t{3}\t{4}", Min, stat.Min(level), stat.Max(level), Max, Mean);
		}

	}

	public class Stat<T, Y> where T: new()
	{
		protected T[] m_sorted = null;
		//protected Func<T, Y> m_accessor = null;
		
		public Stat(IEnumerable<T> seq, Func<T, Y> accessor)
		{
			//m_accessor = accessor;
			m_sorted = seq.OrderBy(l => accessor(l)).ToArray();
		}

		protected int Step(double level)
		{
			return Convert.ToInt32(m_sorted.Length * (1 - level));
		}

		public T Min()
		{
			return m_sorted[0];
		}

		public T Min(double level)
		{
			if (level < 0 || level > 1)
				return new T();

			return m_sorted[Step(level)];
		}

		public T Max()
		{
			return m_sorted[m_sorted.Length - 1];
		}

		public T Max(double level)
		{
			if (level < 0 || level > 1)
				return new T();

			return m_sorted[m_sorted.Length - 1 - Step(level)];
		}

		public void MinMax(double part, out T min, out T max)
		{
			if (part < 0 || part > 1)
			{
				min = new T();
				max = new T();
				return;
			}

			int step = Step(part);
			min = m_sorted[step];
			max = m_sorted[m_sorted.Length - 1 - step];
		}
	}
}
