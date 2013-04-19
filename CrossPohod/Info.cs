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

	public class TimeStat
	{
		public TimeSpan Min = TimeSpan.MaxValue;
		public TimeSpan Max = TimeSpan.MinValue;
		public TimeSpan Total = TimeSpan.Zero;
		public int Num 	{get; protected set;}

		static double Part = 0.95;

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

		protected int Step
		{
			get { return Convert.ToInt32(all.Count * (1 - Part) / 2 );  }
		}
		
		public TimeSpan Min95
		{ 
			get { return all.OrderBy(i => i.Ticks).ElementAt(Step); }		
		}
		public TimeSpan Max95
		{
			get { return all.OrderByDescending(i => i.Ticks).ElementAt(Step); }
		}

		public static string Header()
		{
			return "min\tm95\tM95\tMax\tсредн";
		}

		public String Print()
		{
			if (Num == 0)
				return "0\t0\t0\t0\t0";
			return string.Format("{0}\t{1}\t{2}\t{3}\t{4}", Min, Min95, Max95, Max, Mean);
		}
	}
}
