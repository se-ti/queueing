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

        public BaseInfo(LogItem item): 
            this(item.Work, item.Wait, item.Reject)
        { 
        }
	}

	public class TeamPhaseInfo : BaseInfo
	{
		public Phase Phase;
		public TimeSpan Transit;
		public TeamPhaseInfo(Phase phase, BaseInfo bi, TimeSpan transit)
			: base(bi)
		{
			Phase = phase;
			Transit = transit;
		}
	}

	public class PhaseTeamInfo : BaseInfo
	{
		public Team Team;
		public DateTime When;
		public PhaseTeamInfo(LogItem item)
			: base(item)
		{
			Team = item.Team;
			When = item.Appear;
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

		public static string Header(double level)
		{
			return String.Format("min\tm{0}\tM{0}\tMax\tсредн", Convert.ToInt32(level * 100));
		}

		public String Print(double level)
		{
			if (Num == 0)
				return "0\t0\t0\t0\t0";

			Stat<TimeSpan, TimeSpan, long> stat = new Stat<TimeSpan, TimeSpan, long>(all, ts => ts, ts => ts.Ticks);
            return string.Format("{0:h\\:mm}\t{1:h\\:mm}\t{2:h\\:mm}\t{3:h\\:mm}\t{4}", Min, stat.Min(level), stat.Max(level), Max, Mean);
		}

	}

	/// <summary>
	/// класс для вычисления квантилей на списках
	/// </summary>
	/// <typeparam name="Elem">тип элемента списка</typeparam>
	/// <typeparam name="Val">тип интересующего значения</typeparam>
	/// <typeparam name="SortBy">тип значения, по которому сортировать</typeparam>
	public class Stat<Elem, Val, SortBy> where Elem: new()
	{
		protected Elem[] m_sorted = null;
		protected Func<Elem, Val> m_accessor = null;
		
		public Stat(IEnumerable<Elem> seq, Func<Elem, Val> acc, Func<Elem, SortBy> sorter)
		{
			m_accessor = acc;
			m_sorted = seq.OrderBy(l => sorter(l)).ToArray();
		}

		protected int Step(double level)
		{
			return Convert.ToInt32(m_sorted.Length * (1 - level));
		}

		public Val Min()
		{
			if (m_sorted.Length == 0)
				return m_accessor(new Elem());

			return m_accessor(m_sorted[0]);
		}

		public Val Min(double level)
		{
			if (level < 0 || level > 1 || m_sorted.Length == 0)
				return m_accessor(new Elem());

			int pos = Step(level);
			if (pos < 0 || pos >= m_sorted.Length)
				throw new Exception(String.Format("Min.Out Of Bounds, level {0}, pos {1}, len {2}", level, pos, m_sorted.Length));

			return m_accessor(m_sorted[pos]);
		}

		public Val Max()
		{
			if (m_sorted.Length == 0 )
				return m_accessor(new Elem());
			return m_accessor(m_sorted[m_sorted.Length - 1]);
		}

		public Val Max(double level)
		{
			if (level < 0 || level > 1 || m_sorted.Length == 0)
				return m_accessor(new Elem());

			int pos = m_sorted.Length - 1 - Step(level);
			if (pos < 0 || pos >= m_sorted.Length)
				throw new Exception(String.Format("Max.Out Of Bounds, level {0}, pos {1}, len {2}", level, pos, m_sorted.Length));

			return m_accessor(m_sorted[pos]);
		}

		public void MinMax(double level, out Val min, out Val max)
		{
			min = Min(level);
			max = Max(level);
		}

		public Val Median()
		{
			if (m_sorted.Length == 0)
				return m_accessor(new Elem());

			return m_accessor(m_sorted[m_sorted.Length / 2]);
		}
	}
}
