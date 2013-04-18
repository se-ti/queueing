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


		public void Add(TimeSpan span)
		{
			if (span < Min)
				Min = span;
			if (span > Max)
				Max = span;

			Total += span;
			Num++;
		}

		public TimeSpan Mean 
		{
			get { return new TimeSpan(0, 0, 0, Num != 0 ? (int)(Total.TotalSeconds / Num) : 0); }
		}

		public String Print()
		{
			if (Num == 0)
				return "0\t0\t0";
			return string.Format("{0}\t{1}\t{2}", Min, Max, Mean);
		}
	}
}
