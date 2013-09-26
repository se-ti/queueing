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


        [XmlIgnore]
        public TimeSpan Before;
        [XmlIgnore]
        public TimeSpan After;


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

        [XmlAttribute("Before")]
        public int XmlBefore
        {
            get { return Convert.ToInt32(Before.TotalMinutes); }
            set { Before = new TimeSpan(0, value, 0); }
        }

        [XmlAttribute("After")]
        public int XmlAfter
        {
            get { return Convert.ToInt32(After.TotalMinutes); }
            set { After = new TimeSpan(0, value, 0); }
        }

		public PhaseParam()
		{
			Min = TimeSpan.Zero;
			Mean = TimeSpan.Zero;
			Max = TimeSpan.Zero;
			Sigma = 0.2;
            Before = TimeSpan.Zero;
            After = TimeSpan.Zero;
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

	public static class NodeFactory
	{
		public static Node Create(Phase p)
		{
			switch (p.PType)
			{
				case PhaseType.Start : return new StartNode(p);
				case PhaseType.Finish: return new FinishNode(p);
				default: return new Node(p);
			}
		}
	}

    public class LogItem
    {
        public DateTime Appear = DateTime.MinValue;
        public TimeSpan Before = TimeSpan.Zero;
        public TimeSpan Wait = TimeSpan.Zero;
        public TimeSpan Work = TimeSpan.Zero;
        public TimeSpan After = TimeSpan.Zero;
        public bool Reject = false;

        public CPEvent evt;

        public LogItem(CPEvent e)
        {
            Appear = e.Time;
            evt = e;
        }

        public Team Team { get { return evt.Team; } }
        
        public DateTime Time { get { return evt.Time; } }

        public bool Working { get { return evt != null && evt.EType == EventType.End; } }
    }

	public class Node: Phase
	{
		public Node() { }
		public Node(Phase p) :base(p) {}

		public Dictionary<int, Node> Links = new Dictionary<int, Node>();
		public void Add(int grade, Node phase)
		{
			Links[grade] = phase;
		}

        public void Setup(bool unlimited, bool ignoreTransit)
        {
            bool nonTech = PType != PhaseType.Tech;

            Unlimited = nonTech || unlimited;

            if (nonTech || ignoreTransit)
            {
                Times.Before = TimeSpan.Zero;
                Times.After = TimeSpan.Zero;
            }
        }

        protected bool Unlimited;

		protected List<LogItem> Process = new List<LogItem>();  // Список событий. Пока -- моментов финиша на этапе
		protected Queue<LogItem> Wait = new Queue<LogItem>();   // Очередь ожидания отсечки на этапе.

		protected CPEvent m_next = null;                    // ближайшее событие на этапе
		public CPEvent Next { get { return m_next; } }

        // статистика текущего прогона
		protected int m_maxLoad = 0;
		protected DateTime m_start = DateTime.MaxValue;
		protected DateTime m_end = DateTime.MinValue;

		protected List<PhaseInfo> PInfo = new List<PhaseInfo>();    // статистика этапа по прогонам
		protected List<PhaseTeamInfo> Info = new List<PhaseTeamInfo>();

		protected void CheckStart(DateTime time)
		{
			if (time < m_start)
				m_start = time;
		}

        protected void CheckEnd(DateTime time)
        {
            if (time > m_end)
                m_end = time;
        }

		protected void UpdateNext()
		{
            m_next = null;
            foreach (var e in Process)
                if (m_next == null || e.Time < m_next.Time)
                    m_next = e.evt;
		}

        protected void UpdateMaxLoad()
        {
            int load = Wait.Count + Process.Count(it => it.Working);
            if (load > m_maxLoad)
                m_maxLoad = load;
        }

        private void AddItem(LogItem item)
        {
            Process.Add(item);
            UpdateNext();
        }

        private LogItem GetItem(CPEvent evt)
        {
            var logItem = Process.FirstOrDefault(it => it.evt == evt);
            if (logItem == null)
                throw new Exception(String.Format("На этапе {0} нет команды {1}", Name, evt.Team.Name));
            Process.Remove(logItem);
            UpdateNext();
            return logItem;
        }
        
        public void AddTeam(Random r, Team team, DateTime when)
        {
            ProcessEvent(r, new CPEvent(this, team, when, EventType.Appear));
        }

        public virtual void ProcessEvent(Random r, CPEvent evt)
        {
            switch (evt.EType)
            { 
                case EventType.Appear:                  // подготовиться и встать на отсечку
                    OnAppear(r, evt);
                    break;

                case EventType.Start:                   // стартовать
                    OnTryStart(r, evt);
                    break;

                case EventType.End:                     // закончить работу
                    OnEnd(r, evt);
                    break;

                case EventType.Leave:                   // потормозить в конце
                    OnLeave(r, evt);
                    break;
            }
        }

        // команда появляется на этапе
        protected virtual void OnAppear(Random r, CPEvent evt)
        {
            CheckStart(evt.Time);
            var item = new LogItem(evt);

            item.Before = Scale(Times.Before, item.Team.Smartness);
            evt.EType = EventType.Start;
            evt.Time += item.Before;
            AddItem(item);
        }

        // команда хочет начать работать
        protected virtual void OnTryStart(Random r, CPEvent evt)
        {         
            var logItem = GetItem(evt);

            if (!Unlimited && Channels > 0 && Process.Count(it => it.Working) >= Channels)
                Wait.Enqueue(logItem);
            else
                OnStart(r, logItem);

            UpdateMaxLoad();
        }

        // команда реально начала работать
        protected virtual void OnStart(Random r, LogItem item)
        {
            item.Wait = item.Time - item.Appear - item.Before;  // отсечку можно вычислить только по факту

            TimeSpan dur = NextMoment(r, item.Team);

            item.Reject = Times.Max != TimeSpan.Zero && dur > Times.Max;
            if (item.Reject)
                dur = Times.Max;
            
            item.Work = dur;

            item.evt.Time += dur;
            item.evt.EType = EventType.End;
            AddItem(item);
        }

        protected virtual void OnEnd(Random r, CPEvent evt)
        {
            var item = GetItem(evt);

            LogTeam(item);

            if (Wait.Any())     // запустили первого из стоящих в очереди
            {
                var e = Wait.Dequeue();
                e.evt.Time = evt.Time;
                OnStart(r, e);
            }

            item.After = Scale(Times.After, 1.0 * item.Work.TotalMinutes / Times.Mean.TotalMinutes); // как поработали, так и тормозим
            item.evt.Time += item.After;
            item.evt.EType = EventType.Leave;
            AddItem(item);        
        }

        protected virtual void OnLeave(Random r, CPEvent evt)
        {
            var item = GetItem(evt);
            evt.Team.AddPhase(this, item);

            ProcessNextPhase(r, evt);
        }

        private void ProcessNextPhase(Random r, CPEvent evt)
        {
            Node next;
            Links.TryGetValue(evt.Team.Grade, out next);
            if (next == null)
                throw new Exception(String.Format("Обрыв цепочки на этапе '{0}', PType='{1}', для класса {2}", Name, PType, evt.Team.Grade));

            next.AddTeam(r, evt.Team, evt.Time);
        }

        protected virtual void LogTeam(LogItem item)
        {
            CheckEnd(item.Time);

            PhaseTeamInfo pi = new PhaseTeamInfo(item);
            Info.Add(pi);        
        }

		public PhaseTeamInfo GetTeamInfo(Team t)
		{
			return Info.FirstOrDefault(i => i.Team == t);
		}

        #region basic stat
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

		protected virtual void DetailStatHead(TextWriter tw, StringBuilder sb)
		{
			tw.Write(Name + "\tотсечка");
			sb.AppendFormat("\tработа");
		}

		protected void DetailStatBody(TextWriter tw, StringBuilder sb, IEnumerable<Team> teams)
		{
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
		}

		public virtual void PrintDetailStat(TextWriter tw, IEnumerable<Team> teams)
		{
			StringBuilder sb = new StringBuilder();

			DetailStatHead(tw, sb);
			DetailStatBody(tw, sb, teams);
			
			tw.WriteLine();
			tw.WriteLine(sb.ToString());
		}

		protected virtual void PrintTeamStat(TextWriter tw, StringBuilder sb, PhaseTeamInfo info)
		{
			tw.Write("\t");
			if (info.Wait != TimeSpan.Zero)
				tw.Write("{0:h\\:mm}", info.Wait);
			sb.AppendFormat("\t{0:h\\:mm}", info.Time);
		}

		public static void PrintDetailStatHeader(TextWriter tw, IEnumerable<Team>teams)
		{
			tw.Write("\nЭтап\t");
			foreach (var t in teams)
				tw.Write("\t{0}", t.Name);
			tw.WriteLine();
		}

        protected TimeSpan Scale(TimeSpan span, double scale)
        {
            return new TimeSpan(0, Convert.ToInt32(span.TotalMinutes * scale), 0);
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

        #endregion
    }

	public class StartNode : Node
	{
		public StartNode(Phase p) : base(p) { }

		protected override void DetailStatHead(TextWriter tw, StringBuilder sb)
		{
			tw.Write(Name + "\tВремя старта");
			sb.AppendFormat("\tработа");
		}

		protected override void PrintTeamStat(TextWriter tw, StringBuilder sb, PhaseTeamInfo info)
		{
			tw.Write("\t{0:h\\:mm}", info.When.TimeOfDay);
			sb.AppendFormat("\t{0:h\\:mm}", info.Time);
		}

        protected override void OnStart(Random r, LogItem item)
        {
            base.OnStart(r, item);
            item.Reject = false;    // на старте нет снятий :)
        }
	}

	public class FinishNode : Node
	{
		public FinishNode(Phase p) : base(p) { }

		protected override void DetailStatHead(TextWriter tw, StringBuilder sb)
		{
			tw.Write(Name + "\tработа");
			sb.AppendFormat("\tВремя финиша");
		}

		public override void PrintDetailStat(TextWriter tw, IEnumerable<Team> teams)
		{
			base.PrintDetailStat(tw, teams);
			tw.WriteLine();
		}

		protected override void PrintTeamStat(TextWriter tw, StringBuilder sb, PhaseTeamInfo info)
		{
			tw.Write("\t{0:h\\:mm}", info.Time);
			sb.AppendFormat("\t{0:h\\:mm}", info.When.TimeOfDay);
		}

        protected override void LogTeam(LogItem item)
        {
            CheckEnd(item.Time);

            PhaseTeamInfo pi = new PhaseTeamInfo(item)
            {
                When = item.Appear + item.Before + item.Wait + item.Work
            };

            Info.Add(pi);
        }

        public override void ProcessEvent(Random r, CPEvent evt)
        {
            CheckStart(evt.Time);
            var item = new LogItem(evt);
            LogTeam(item);
            evt.Team.AddPhase(this, item);
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

            return String.Format("{0}\t{1}\t{2}\t{3}\t{4:h\\:mm}\t{5:h\\:mm}\t{6:h\\:mm}\t{7:h\\:mm}\t{8}\t{9}\t{10}\t{11}\t{12}", name, Teams / n, load.Max(), load.Max(level), start.Min(), start.Min(level), end.Max(level), end.Max(), Time.Print(level), Div(Wait.Num, n), Wait.Print(level), Div(Rejects, n), max != TimeSpan.Zero ? max.ToString(@"h\:mm") : "");
		}

		private static decimal Div(int n, int m)
		{
			return decimal.Round(decimal.Divide(n, m), 2);
		}
	}
}
