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

		protected List<PhaseTeamInfo> Info = new List<PhaseTeamInfo>(); 

		protected void CheckStart(DateTime time)
		{
			if (time < m_start)
				m_start = time;
		}

		public void TeamLeave(Random r, CPEvent evt)
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
				AddTeam(r, e.Team, evt.Time, evt.Time - e.Time);
			}
		}

		public void AddTeam(Random r, CPEvent e, bool unlimited)
		{
			CheckStart(e.Time);

			int load = Wait.Count + Process.Count + 1;
			if (load > m_maxLoad)
				m_maxLoad = load;

			if (!unlimited && Channels > 0 && Process.Count >= Channels)
				Wait.Enqueue(new CPEvent(this, e.Team, e.Time));
			else
				AddTeam(r, e.Team, e.Time, TimeSpan.Zero);
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


			if (evt.Time < When)
			{
				m_when = evt.Time;
				m_next = evt;
			}
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

		public PhaseStat GetStat()
		{
			return new PhaseStat(Info, m_start, m_end, m_maxLoad);
		}

		public static string PrintHeader()
		{
			return "Этап\t" + PhaseStat.PrintHeader();
		}

		public string PrintStat(int n)
		{
			return GetStat().PrintStat(Name, n);
		}

		public void PrintDetailStat(TextWriter tw, IEnumerable<Team> teams)
		{
			tw.Write(Name);
			StringBuilder sb = new StringBuilder();
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
			if (PType == PhaseType.Start)
			{
				tw.Write("\t{0}", info.When.TimeOfDay);
				sb.AppendFormat("\t{0}", info.Time);
			}
			else if (PType == PhaseType.Finish)
			{
				tw.Write("\t{0}", info.Time);
				sb.AppendFormat("\t{0}", info.When.TimeOfDay);
			}
			else
			{
				tw.Write("\t{0}", info.Time);
				sb.Append("\t");
				if (info.Wait != TimeSpan.Zero)
					sb.AppendFormat("{0}", info.Wait);
			}
		}

		public static void PrintDetailStatHeader(TextWriter tw, IEnumerable<Team>teams)
		{
			tw.Write("Этап");
			foreach (var t in teams)
				tw.Write("\t{0}", t.Name);
			tw.WriteLine();
		}

		protected TimeSpan NextMoment(Random r, Team t)
		{
			//t.Smartness -- коэффициент быстроходности / тормознутости
			//t.Members -- число человек
			//time	-- дольше идем -- больше устаем

			// this.Times -- характеристики этапа Min, Mean, Max, Sigma
			// this.PType -- тип этапа (тех / перегон)

			double minutes = GetNormalDistibutedRandom(r, Times.Mean.TotalMinutes * t.Smartness, Times.Sigma);

			if (minutes < Times.Min.TotalMinutes)
				minutes = Times.Min.TotalMinutes;

			if (minutes < 0.0)
				minutes = 0.0;

			return new TimeSpan(0, Convert.ToInt32(minutes), 0);
		}

		/*!	@return	случайное число с нормальным (Гауссовым) распределением
		 * @param afExpected -- матожидание
		 * @param afDeviation -- нормированное среднекватратичное отклонение
		 * 
		 * 	На основании центральной предельной теоремы имеем в NewRandom случайную величину 
		 * 	с распределением, близким к нормальному с параметрами M=0, Sigma = 1
		 * 	12 выбрано ради получения Sigma = 1.
		 * */
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
		DateTime Start = DateTime.MaxValue;
		DateTime End = DateTime.MinValue;
		int Rejects = 0;

		TimeStat Time = new TimeStat();
		TimeStat Wait = new TimeStat();
		int MaxLoad = 0;
		int Teams = 0;

		public PhaseStat(List<PhaseTeamInfo> info, DateTime start, DateTime end, int maxLoad)
		{
			Start = start;
			End = end;
			MaxLoad = maxLoad;

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

		public static string PrintHeader()
		{
			return String.Format("Команд\tмакс заг\tзанят с\tпо\tработа {0}\t отсечек\t{0}\tснятий", TimeStat.Header());
		}
		public string PrintStat(string name, int n)
		{
			return String.Format("{0}\t{1}\t{2}\t{3}\t{4}\t{5}\t{6}\t{7}\t{8}", name, Teams / n, MaxLoad, Start.TimeOfDay, End.TimeOfDay, Time.Print(), Div(Wait.Num, n), Wait.Print(), Div(Rejects, n));
		}

		private static decimal Div(int n, int m)
		{
			return decimal.Round(decimal.Divide(n, m), 2);
		}
	}
}
