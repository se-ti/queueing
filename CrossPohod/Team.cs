using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.Xml.Serialization;

namespace CrossPohod
{
	[Serializable]
	public class Team
	{
		[XmlAttribute]
		public string Name;
		[XmlAttribute]
		public int Members;
		[XmlAttribute]
		public int Grade;

		[XmlAttribute]
		public double Smartness = 0.5;
 
		[XmlIgnore]
		public List<TeamPhaseInfo> Info = new List<TeamPhaseInfo>();

		List<TeamStat> m_stat = new List<TeamStat>();
		[XmlIgnore]
		public TeamStat TotalStat
		{
			get
			{
				if (m_stat == null)
					m_stat = new List<TeamStat>(new [] { new TeamStat(Info){Start = Start} });
				
				var res = new TeamStat();
				foreach (var ts in m_stat)
					res += ts;
				return res;
			}
		}

		protected DateTime Start;

		public Team()
		{
			Name = string.Empty;
			Members = 1;
			Grade = 0;
			Smartness = 0.5;
		}

		public void AddPhase(Phase p, BaseInfo bi)
		{
			Info.Add(new TeamPhaseInfo(p, bi));
		}

		public TeamStat GetStat(int day)
		{
			if (m_stat == null)
				m_stat = new List<TeamStat>();

			if (day == m_stat.Count)
			{
				TeamStat stat = new TeamStat(Info) { Start = this.Start };
				m_stat.Add(stat);
				Info.Clear();
			}

			if (day >= 0 && day < m_stat.Count)
				return m_stat[day];

			throw new Exception(String.Format("В команде {0} нет статистики на день {1}", Name, day));
		}

		public void ClearStat()
		{
			m_stat = null;
		}
		public TeamStat RecomputeStat()
		{
			ClearStat();
			return TotalStat;
		}

		public void SetStart(DateTime time)
		{
			Start = time;
		}

		public static string PrintHeader()
		{
			return "Команда\t" + TeamStat.StatHeader();
		}
		public string PrintStat()
		{
			return String.Format("{0}\t{1}", Name, TotalStat.ToString());
		}
	}

	public class TeamStat
	{
		public DateTime Start = DateTime.MinValue; 
		public TimeSpan Time = TimeSpan.Zero;
		public TimeSpan Wait = TimeSpan.Zero;
		
		public TimeSpan Total
		{
			get { return Time + Wait; }
		}

		public DateTime Finish
		{
			get { return Start + Total; }
		}

		// число снятий
		public int Reject = 0;

		// число отсечек 
		public int Waits = 0;

		protected string RejectComment = "";
		protected string WaitComment = "";


		public TeamStat()
		{ 
		
		}
		public TeamStat(List<TeamPhaseInfo> info)
		{			
			foreach (var i in info)
			{
				Time += i.Time;
				Wait += i.Wait;
				if (i.Reject)
				{
					Reject++;
					RejectComment += " " + i.Phase.Name;
				}
				if (i.Wait != TimeSpan.Zero)
				{
					Waits++;
					WaitComment += " " + i.Phase.Name;
				}
			}
		}

		public static TeamStat operator + (TeamStat a, TeamStat b)
		{
			var r = new TeamStat();
			r.Start = a.Start;

			r.Time = a.Time + b.Time;
			r.Wait = a.Wait + b.Wait;
			r.Reject = a.Reject + b.Reject;
			r.Waits = a.Waits + b.Waits;
			r.RejectComment = a.RejectComment + b.RejectComment;
			r.WaitComment = a.WaitComment + b.WaitComment;

			return r;
		}

		public static string StatHeader()
		{
			return "Старт\tРабота\tОтсечек\tНа отс.\tСнятий\tСнятия\tОтсечки\t";
		}
		public override string ToString()
		{
			return String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}", Start.TimeOfDay, Time.ToString(), Waits, Wait.ToString(), Reject, RejectComment, WaitComment);
		}
	}
}
