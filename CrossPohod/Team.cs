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

		public void AddPhase(Phase p, LogItem item)
		{
			Info.Add(new TeamPhaseInfo(p, new BaseInfo(item), item.Before + item.After));
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

		public void SetStart(DateTime time)
		{
			Start = time;
		}

		public static string PrintHeader()
		{
			return "Команда\t" + TeamStat.StatHeader(true) + TeamStat.StatHeader(true) + TeamStat.StatHeader(false);
		}
		public string PrintStat()
		{
			string stats = "";
			foreach (var s in m_stat)
				stats += String.Format("{0}\t", s.ToString(true));

			return String.Format("{0}\t{1}{2}", Name, stats, TotalStat.ToString(false));
		}
	}

	public class TeamStat
	{
		public DateTime Start = DateTime.MinValue; 
		public TimeSpan Time = TimeSpan.Zero;
		public TimeSpan Transit = TimeSpan.Zero;
		public TimeSpan Wait = TimeSpan.Zero;
		
		public TimeSpan Total
		{
			get { return Time + Transit + Wait; }
		}

		public TimeSpan Work
		{
			get { return Time + Transit; }
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
				Transit += i.Transit;
				Wait += i.Wait;
				if (i.Reject)
				{
					RejectComment += (Reject <= 0 ? "" : ", ") + i.Phase.Name;
					Reject++;
				}
				if (i.Wait != TimeSpan.Zero)
				{
					WaitComment += (Waits <= 0 ? "" : ", ") + i.Phase.Name;
					Waits++;
				}
			}
		}

		protected static bool NeedJoin(string a, string b)
		{
			return !String.IsNullOrEmpty(a) && !String.IsNullOrEmpty(b);
		}

		protected static string Join(string a, string b)
		{
			return a + (NeedJoin(a, b) ? ", " : "") + b;
		}

		public static TeamStat operator + (TeamStat a, TeamStat b)
		{
			var r = new TeamStat();
			r.Start = a.Start != DateTime.MinValue ? a.Start : b.Start;

			r.Time = a.Time + b.Time;
			r.Transit = a.Transit + b.Transit;
			r.Wait = a.Wait + b.Wait;
			r.Reject = a.Reject + b.Reject;
			r.Waits = a.Waits + b.Waits;
			r.RejectComment = Join(a.RejectComment, b.RejectComment);
			r.WaitComment = Join(a.WaitComment, b.WaitComment);

			return r;
		}

		public static string StatHeader(bool reduced)
		{
			return reduced ? 
				"Cтарт\tРабота\tТранзитка\tОтсечек\tНа отс.\tСнятий\t" :
				"Работа\tТранзитка\tОтсечек\tНа отс.\tСнятий\tСнятия\tОтсечки\t";
		}
		public string ToString(bool reduced)
		{
			string s = String.Format("{0}\t{1}\t{2}\t{3}\t{4}", Time.ToString(), Transit.ToString(), Waits, Wait.ToString(), Reject);

			return reduced ? String.Format("{0}\t{1}", Start.TimeOfDay, s) : String.Format("{0}\t{1}\t{2}", s, RejectComment, WaitComment);
		}
	}
}
