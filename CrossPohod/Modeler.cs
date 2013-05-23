using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Xml.Serialization;

namespace CrossPohod
{
	public class CPEvent
	{
		public DateTime Time;
		public Team Team;
		public Node Node;

		public CPEvent(Node node, Team team, DateTime time)
		{
			Time = time;
			Team = team;
			Node = node;
		}
	}

	public class ModelerParams
	{
		public string Source = "source.xml";
		public double Level = 0.95;
		public bool Unlimited = false;
		public TimeSpan Before = TimeSpan.Zero;
		public TimeSpan After = TimeSpan.Zero;
	}

	[Serializable]
	public class Modeler
	{

		[Serializable]
		public class Link
		{
			public Link() { }

			[XmlAttribute]
			public int Grade = 0;
			public List<string> Sequence = new List<string>();
		}

		public Modeler()
		{
			Teams = new List<Team>();
			Phases = new List<Phase>();
			Links = new List<Link>();
			Nodes = new Dictionary<string, Node>();

			Unlimited = false;
		}

		public List<Team> Teams;
		public List<Phase> Phases;
		public List<Link> Links;

		[XmlIgnore]
		public Dictionary<string, Node> Nodes;

		[XmlIgnore]
		public bool Unlimited = false;

		protected double m_level = 0.95;
		[XmlIgnore]
		public double Level 
		{
			get {return m_level;}
			set 
			{
				if (value <= 0 || value > 1)
					throw new ArgumentOutOfRangeException("Modeler.Level", value, "Значение вне диапазона (0; 1]");
				m_level = value;
			}
		}

		[XmlIgnore]
		public TimeSpan Before = TimeSpan.Zero;
		[XmlIgnore]
		public TimeSpan After = TimeSpan.Zero;

		//protected Dictionary<string, List<TeamStat>> TeamStat = new Dictionary<string, List<TeamStat>>();
		//protected Dictionary<string, List<PhaseStat>> PhaseStat = new Dictionary<string, List<PhaseStat>>();


		public void RetrieveTeamStat(int day)
		{
			foreach (var t in Teams)
				t.GetStat(day);			
		}

		public void PhaseStats(TextWriter tw, int num)
		{
			tw.WriteLine();

			if (num > 1)
			{
				tw.WriteLine(Node.PrintHeader(Level));
				foreach (var node in Nodes.Values.Where(n => n.PType != PhaseType.Pass))
					tw.WriteLine(node.PrintStat(num, Level));
			}
			else
			{
				List<Team> teams = null;
				foreach (var node in Nodes.Values)
				{
					if (node.PType == PhaseType.Start || teams == null)
					{
						if (node.PType == PhaseType.Start)
							teams = Teams.OrderBy(t => t.Grade)
										 .ThenBy(t => { var i = node.GetTeamInfo(t); return i != null ? i.When : DateTime.MaxValue; })
										 .ToList();
						else
							teams = Teams;
						
						Node.PrintDetailStatHeader(tw, teams);
					}

					node.PrintDetailStat(tw, teams);
				}
			}
		}

		public void Model(Random r)
		{
			CPEvent evt;
			while (true)
			{
				evt = GetEvent();
				if (evt == null)
					break;
				Process(r, evt);
			}
		}

		protected void Process(Random r, CPEvent pe)
		{
			pe.Node.TeamLeave(r, pe, Before);

			if (pe.Node.PType == PhaseType.Finish)
				return;

			Node next;
			pe.Node.Links.TryGetValue(pe.Team.Grade, out next);
			if (next == null )
				throw new Exception(String.Format("Обрыв цепочки на этапе '{0}', PType='{1}', для класса {2}", pe.Node.Name, pe.Node.PType, pe.Team.Grade));

			CPEvent ne = pe;
			if (pe.Node.PType == PhaseType.Tech)
				ne = new CPEvent(pe.Node, pe.Team, pe.Time + After);

			next.AddTeam(r, ne, Unlimited, Before);
		}

		protected CPEvent GetEvent()
		{
			CPEvent evt = null;
			foreach (var p in Nodes.Values)
				if (p.Next != null && (evt == null || evt.Time > p.Next.Time))
					evt = p.Next;

			return evt;
		}


		#region Serialize
		public static Modeler Read(ModelerParams param)
		{
			var fstream = new FileStream(param.Source, FileMode.Open, FileAccess.Read);
			var xmlFormat = new XmlSerializer(typeof(Modeler), SerializerTypes);

			var m = xmlFormat.Deserialize(fstream) as Modeler;
			fstream.Close();

			m.SetupLinks();

			m.Level = param.Level;
			m.Unlimited = param.Unlimited;
			m.Before = param.Before;
			m.After = param.After;
			return m;
		}

		public void Write(string file)
		{
			var fstream = new FileStream(file, FileMode.Create, FileAccess.Write);
			var xmlFormat = new XmlSerializer(typeof(Modeler), Modeler.SerializerTypes);

			xmlFormat.Serialize(fstream, this);
			fstream.Close();
		}

		protected void SetupLinks()
		{
			Nodes = new Dictionary<string, Node>(Phases.Select(p => new Node(p)).ToDictionary(n => n.Name, n => n));

			Node src, dest;
			foreach (var link in Links)
			{
				src = NodeByKey(link.Sequence.First());
				foreach (var seq in link.Sequence.Skip(1))
				{
					dest = NodeByKey(seq);
					src.Add(link.Grade, dest);
					src = dest;
				}
			}
		}

		protected Node NodeByKey(string key)
		{
			if (String.IsNullOrEmpty(key))
				throw new Exception("Пустое поле в списке связей <Link><Sequence>");

			Node res;
			if (!Nodes.TryGetValue(key, out res) || res == null)
				throw new Exception(String.Format("Нет этапа '{0}', проверьте соответствие этапов и полей Link Sequence", key));

			return res;
		}

		private static Type[] SerializerTypes = new Type[] { typeof(Team), typeof(Phase), typeof(PhaseParam), typeof(TimeSpan), typeof(List<Team>), typeof(List<Phase>), typeof(Modeler.Link), typeof(List<Modeler.Link>), typeof(List<string>) };
		#endregion
	}
}
