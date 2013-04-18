using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Xml.Serialization;

namespace CrossPohod
{
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

		protected Dictionary<string, List<TeamStat>> TeamStat = new Dictionary<string, List<TeamStat>>();
		protected Dictionary<string, List<PhaseStat>> PhaseStat = new Dictionary<string, List<PhaseStat>>();

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
				tw.WriteLine(Node.PrintHeader());
				foreach (var node in Nodes.Values.Where(n => n.PType != PhaseType.Pass))
					tw.WriteLine(node.PrintStat(num));
			}
			else
			{
				Node.PrintDetailStatHeader(tw, Teams);
				foreach (var node in Nodes.Values)
					node.PrintDetailStat(tw, Teams);
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
			pe.Node.TeamLeave(r, pe);

			if (pe.Node.PType == PhaseType.Finish)
				return;

			Node next;
			pe.Node.Links.TryGetValue(pe.Team.Grade, out next);
			if (next == null )
				throw new Exception(String.Format("Обрыв цепочки на этапе '{0}', PType='{1}', для класса {2}", pe.Node.Name, pe.Node.PType, pe.Team.Grade));

			next.AddTeam(r, pe, Unlimited);
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
		public static Modeler Read(string file)
		{
			var fstream = new FileStream(file, FileMode.Open, FileAccess.Read);
			var xmlFormat = new XmlSerializer(typeof(Modeler), SerializerTypes);

			var m = xmlFormat.Deserialize(fstream) as Modeler;
			fstream.Close();

			m.SetupLinks();
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
