using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Xml.Serialization;

namespace CrossPohod
{
	public enum PhaseType
	{
		Start,
		Tech,
		Pass,
		Finish
	}

	#region serialization classes
	[Serializable]
	public class PhaseParam
	{
		[XmlIgnore]
		public TimeSpan Min;
		[XmlIgnore]
		public TimeSpan Mean;
		[XmlIgnore]
		public TimeSpan Max;

		[XmlAttribute]
		public double Sigma;

		[XmlAttribute("Min")]
		public int XmlMin
		{
			get { return Convert.ToInt32(Min.TotalMinutes); }
			set { Min = new TimeSpan(0, value, 0); }
		}

		[XmlAttribute("Mean")]
		public int XmlMean
		{
			get { return Convert.ToInt32(Mean.TotalMinutes); }
			set { Mean = new TimeSpan(0, value, 0); }
		}

		[XmlAttribute("Max")]
		public int XmlMax
		{
			get { return Convert.ToInt32(Max.TotalMinutes); }
			set { Max = new TimeSpan(0, value, 0); }
		}

		public PhaseParam()
		{
			Min = TimeSpan.Zero;
			Mean = TimeSpan.Zero;
			Max = TimeSpan.Zero;
			Sigma = 0.2;
		}
	}

	[Serializable]
	public class Phase
	{
		public Phase() { }
		public Phase(Phase p)
		{
			Name = p.Name;
			Channels = p.Channels;
			Times = p.Times;
			PType = p.PType;
		}

		[XmlAttribute]
		public string Name;

		[XmlAttribute]
		public int Channels = 0;

		// минимальное, среднее, КВ, сигма
		public PhaseParam Times;

		[XmlAttribute]
		public PhaseType PType = PhaseType.Tech;
	}

	#endregion

	public class Node: Phase
	{
		public Node() { }
		public Node(Phase p) :base(p) {}

		public Dictionary<int, Node> Links = new Dictionary<int, Node>();
		public void Add(int grade, Node phase)
		{
			Links[grade] = phase;
		}

		protected List<CPEvent> Process = new List<CPEvent>();
		protected Queue<CPEvent> Wait = new Queue<CPEvent>();

		protected DateTime m_when = DateTime.MaxValue;
		public DateTime When { get { return m_when; } }
		
		protected CPEvent m_next = null;
		public CPEvent Next { get { return m_next; } }

		protected int m_maxLoad = 0;
		protected DateTime m_start = DateTime.MaxValue;
		protected DateTime m_end = DateTime.MinValue;

		protected List<PhaseInfo> PInfo = new List<PhaseInfo>();
		protected List<PhaseTeamInfo> Info = new List<PhaseTeamInfo>();

		protected void CheckStart(DateTime time)
		{
			if (time < m_start)
				m_start = time;
		}

		protected void CheckLeave(CPEvent evt)
		{
			if (evt.Time < When)
			{
				m_when = evt.Time;
				m_next = evt;
			}
		}

		public void TeamLeave(Random r, CPEvent evt, TimeSpan before)
		{
			if (!Process.Remove(evt))
				throw new Exception(String.Format("На этапе {0} нет команды {1}", Name, evt.Team.Name));

			if (evt.Time > m_end)
				m_end = evt.Time;

			this.m_when = DateTime.MaxValue;
			this.m_next = null;
			foreach (var e in Process)
				if (e.Time < m_when)
				{
					m_when = e.Time;
					m_next = e;
				}

			if (Wait.Any())
			{
				var e = Wait.Dequeue();
				TimeSpan prepare = PType != PhaseType.Tech ? TimeSpan.Zero : before;
				TimeSpan wait = evt.Time - e.Time;

				if (prepare > TimeSpan.Zero)	// этап только освободился, а мы уже готовы
				{
					var min = wait < prepare ? wait : prepare;
					wait -= min;
					prepare -= min;
				}

				AddTeam(r, e.Team, evt.Time + prepare, wait);
			}
		}

		public void AddTeam(Random r, CPEvent e, bool unlimited, TimeSpan before)
		{
			CheckStart(e.Time);

			int load = Wait.Count + Process.Count + 1;
			if (load > m_maxLoad)
				m_maxLoad = load;

			TimeSpan prepare = PType != PhaseType.Tech ? TimeSpan.Zero : before;

			if (!unlimited && Channels > 0 && Process.Count >= Channels)
				Wait.Enqueue(new CPEvent(this, e.Team, e.Time));
			else
				AddTeam(r, e.Team, e.Time + prepare, TimeSpan.Zero);
		}

		protected void AddTeam(Random r, Team t, DateTime when, TimeSpan wait)
		{
			TimeSpan dur = NextMoment(r, t);

			bool reject = Times.Max != TimeSpan.Zero && dur > Times.Max;
			if (reject)
				dur = Times.Max;
			
			// логируем
			BaseInfo bi = new BaseInfo(dur, wait, reject);
			t.AddPhase(this, bi);
			PhaseTeamInfo pi = new PhaseTeamInfo(t, bi);
			if (PType == PhaseType.Finish)
				pi.When = when + dur;

			Info.Add(pi); 

			var evt = new CPEvent(this, t, when + dur);
			Process.Add(evt);
			CheckLeave(evt);			
		}

		public void AddToStart(Random r, Team t, DateTime time)	// время старта!!!
		{
			var ts = NextMoment(r, t);
			if (ts > Times.Max)
				ts = Times.Max;									// на старте нет снятий :)

			var evt = new CPEvent(this, t, time+ts);
			Process.Add(evt);

			BaseInfo bi = new BaseInfo(ts, TimeSpan.Zero, false);
			Info.Add(new PhaseTeamInfo(t, bi) {When = time});

			if (m_when == DateTime.MaxValue || m_when > time)
			{
				m_when = evt.Time;
				m_next = evt;
			}

			CheckStart(time);
		}

		public PhaseTeamInfo GetTeamInfo(Team t)
		{
			return Info.FirstOrDefault( i => i.Team == t);
		}

		public void EndOfTurn()
		{
			if (m_maxLoad == 0 && m_start == DateTime.MaxValue)	// nothing happened since last call;
				return; 

			PInfo.Add(new PhaseInfo(m_maxLoad, m_start, m_end));
			m_maxLoad = 0;
			m_end = DateTime.MinValue;
			m_start = DateTime.MaxValue;
		}

		public PhaseStat GetStat()
		{
			EndOfTurn();
			return new PhaseStat(Info, PInfo);
		}

		public static string PrintHeader(double level)
		{
			return "Этап\t" + PhaseStat.PrintHeader(level);
		}

		public string PrintStat(int n, double level)
		{
			return GetStat().PrintStat(Name, n, level, Times.Max);
		}

		public void PrintDetailStat(TextWriter tw, IEnumerable<Team> teams)
		{
			StringBuilder sb = new StringBuilder();

			switch (PType)
			{ 
				case PhaseType.Start:
					tw.Write(Name + "\tВремя старта");
					sb.AppendFormat("\tработа");
					break;
				case PhaseType.Finish:
					tw.Write(Name + "\tработа");
					sb.AppendFormat("\tВремя финиша");
					break;
				default:
					tw.Write(Name + "\tотсечка");
					sb.AppendFormat("\tработа");
					break;
			}

			PhaseTeamInfo info;
			foreach (var t in teams)
			{
				info = Info.FirstOrDefault(i => i.Team == t);	// команда бывает на этапе лишь 1 раз
				if (info != null)
					PrintTeamStat(tw, sb, info);
				else
				{
					tw.Write("\t");
					sb.Append("\t");
				}
			}
			tw.WriteLine();
			tw.WriteLine(sb.ToString());
			if (PType == PhaseType.Finish)
				tw.WriteLine();
		}

		protected void PrintTeamStat(TextWriter tw, StringBuilder sb, PhaseTeamInfo info)
		{
			switch(PType) 
			{
				case PhaseType.Start:
					tw.Write("\t{0}", info.When.TimeOfDay);
					sb.AppendFormat("\t{0}", info.Time);
					break;
				
				case PhaseType.Finish:
					tw.Write("\t{0}", info.Time);
					sb.AppendFormat("\t{0}", info.When.TimeOfDay);
					break;

				default:
					tw.Write("\t");
					if (info.Wait != TimeSpan.Zero)
						tw.Write("{0}", info.Wait);
					sb.AppendFormat("\t{0}", info.Time);
					break;
			}
		}

		public static void PrintDetailStatHeader(TextWriter tw, IEnumerable<Team>teams)
		{
			tw.Write("\nЭтап\t");
			foreach (var t in teams)
				tw.Write("\t{0}", t.Name);
			tw.WriteLine();
		}

		/// <param name="r">генератор равномерно распределенных случайных чисел</param>
		/// <param name="t">командаб t.Smartness -- коэффициент быстроходности / тормознутости t.Members -- число человек</param>
		/// <returns></returns>
		protected TimeSpan NextMoment(Random r, Team t)
		{
			// this.Times -- характеристики этапа Min, Mean, Max, Sigma

			double minutes = GetNormalDistibutedRandom(r, Times.Mean.TotalMinutes * t.Smartness, Times.Sigma);

			if (minutes < Times.Min.TotalMinutes)
				minutes = Times.Min.TotalMinutes;

			if (minutes < 0.0)
				minutes = 0.0;

			return new TimeSpan(0, Convert.ToInt32(minutes), 0);
		}

		/// <summary>
		/// На основании центральной предельной теоремы имеем в NewRandom случайную величину 
		///	с распределением, близким к нормальному с параметрами M=0, Sigma = 1
		///	12 выбрано ради получения Sigma = 1. 
		/// </summary>
		/// <param name="r">генератор равномерно распределенных случайных чисел</param>
		/// <param name="afExpected">матожидание</param>
		/// <param name="afDeviation">нормированное среднекватратичное отклонение</param>
		/// <returns>случайное число с нормальным (Гауссовым) распределением</returns>
		public static double GetNormalDistibutedRandom(Random r, double afExpected, double afDeviation)
		{
			int i;
			const int N = 12;
			double NewRandom = 0.0;
			for (i = 0; i < N; i++)
			{
				NewRandom += r.Next(0, 100000) / 100000.0;
			}
			NewRandom -= N / 2;

			return NewRandom * afDeviation * afExpected + afExpected;
		}
	}

	public class PhaseStat
	{
		int Teams = 0;
		int Rejects = 0;
		TimeStat Time = new TimeStat();
		TimeStat Wait = new TimeStat();

		List<PhaseInfo> PInfo = null;

		public PhaseStat(List<PhaseTeamInfo> info, List<PhaseInfo> pInfo)
		{
			PInfo = pInfo;

			foreach (var i in info)
			{
				Teams++;
				Time.Add( i.Time);
				if (i.Wait != TimeSpan.Zero)
					Wait.Add(i.Wait);
				if (i.Reject)
					Rejects++;
			}
		}

		public static string PrintHeader(double level)
		{
			return String.Format("Команд\tмакс заг\t{0}% макс заг\tзанят с\tс{0}\tпо{0}\tпо\tработа {1}\t отсечек\t{1}\tснятий\tКВ", Convert.ToInt32(level * 100), TimeStat.Header(level));
		}
		public string PrintStat(string name, int n, double level, TimeSpan max)
		{
			Stat<PhaseInfo, int, int> load = new Stat<PhaseInfo, int, int>(PInfo, p => p.MaxLoad, p => p.MaxLoad);
			Stat<PhaseInfo, TimeSpan, long> start = new Stat<PhaseInfo, TimeSpan, long>(PInfo, p => p.Start.TimeOfDay, p => p.Start.Ticks);
			Stat<PhaseInfo, TimeSpan, long> end   = new Stat<PhaseInfo, TimeSpan, long>(PInfo, p => p.End.TimeOfDay,   p => p.End.Ticks);

			return String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}\t{9}\t{10}\t{11}\t{12}", name, Teams / n, load.Max(), load.Max(level), start.Min(), start.Min(level), end.Max(level), end.Max(), Time.Print(level), Div(Wait.Num, n), Wait.Print(level), Div(Rejects, n), max != TimeSpan.Zero ? max.ToString() : "");
		}

		private static decimal Div(int n, int m)
		{
			return decimal.Round(decimal.Divide(n, m), 2);
		}
	}
}
