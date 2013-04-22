using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using Distance = System.Collections.Generic.Dictionary<string, CrossPohod.Node>;

namespace CrossPohod
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 3)
			{
				Program.PrintSyntax();
				return;
			}				
			
//			Program.TestModeler();

			Random r = new Random();
			Modeler mod = Modeler.Read(args[0]);

			FileStream fs = new FileStream(args[1], FileMode.Create, FileAccess.Write);
			StreamWriter sw = new StreamWriter(fs);

			int N;
			if (!Int32.TryParse(args[2], out N))
				N = 1;

			mod.Unlimited = args.Length >= 4 && "-unlim".CompareTo(args[3]) == 0;

			for (int i = 0; i < N; i++)
			{
				if (i%10 == 9)
					Console.WriteLine(i+1);

				Program.StartDay1(r, mod.Nodes["Старт1"], mod.Teams, DateTime.MinValue.AddHours(6));
				mod.Model(r);
				mod.RetrieveTeamStat(0);

				Program.StartDay2(r, mod.Nodes["Старт2"], mod.Teams, DateTime.MinValue.AddDays(1.25));
				mod.Model(r);
				mod.RetrieveTeamStat(1);


				sw.WriteLine(Team.PrintHeader());
				foreach (var t in mod.Teams.OrderBy(tm => tm.Grade).ThenBy(tm => tm.GetStat(0).Start))
				{
					sw.WriteLine(t.PrintStat());
					t.ClearStat();
				}
				foreach (var n in mod.Nodes.Values)
					n.EndOfTurn();

				sw.Flush();
			}

			mod.PhaseStats(sw, N);
			sw.Flush();
			fs.Close();

			Console.Write("Press any key...");
			Console.ReadKey();
		}

		public static void PrintSyntax()
		{
			Console.WriteLine("Syntax:");
			Console.WriteLine("crosspohod configFile outCsv [NNN [-unlim]]");
			Console.WriteLine("\tNNN\tчисло повторений");
			Console.WriteLine("\t-unlim\tрежим оценки необходимой пропускной способности");
		}

		public static void StartDay1(Random r, Node n, List<Team> teams, DateTime start)
		{
			List<Team> a = teams.Where(t => t.Grade >= 2)
								.OrderBy(t => r.Next())			// shuffle!
								.ToList();
			List<Team> b = teams.Where(t => t.Grade < 2)
								.OrderBy(t => r.Next())
								.ToList();

			
			List<Team> join = new List<Team>();
			while (a.Any() && b.Any())
			{
				join.Add(a.First());
				join.Add(b.First());

				a.RemoveAt(0);
				b.RemoveAt(0);
			}

			if (a.Any())
				join.AddRange(a);
			if (b.Any())
				join.AddRange(b);

			int i = 0;
			foreach (var t in join)
			{
				n.AddToStart(r, t, start);
				t.SetStart(start);

				i++;
				if (i % n.Channels == 0)
					start += n.Times.Max;
			}

		}

		public static void StartDay2(Random r, Node n, List<Team> teams, DateTime start)
		{
			List<Team> a = teams.Where(t => t.Grade >= 2).OrderBy(t => t.GetStat(0).Time.TotalSeconds).ToList();
			List<Team> b = teams.Where(t => t.Grade < 2).OrderBy(t => t.GetStat(0).Time.TotalSeconds).ToList();

			while (a.Any() && b.Any())
			{
				Program.AddTeam(r, n, a, start);
				Program.AddTeam(r, n, b, start);

				start += n.Times.Max;
			}

			if (b.Any())	//
				a = b;

			while (a.Any())
			{
				Program.AddTeam(r, n, a, start);
				if (a.Any())
					Program.AddTeam(r, n, a, start);
				start += n.Times.Max;
			}
		}

		public static void AddTeam(Random r, Node n, List<Team> teams, DateTime t)
		{
			var team = teams.First();
			n.AddToStart(r, team, t);
			teams.RemoveAt(0);

			team.SetStart(t);
		}

		public static void TestModeler()
		{
			Modeler mod = null;
			try
			{
				mod = new Modeler();
				mod.Links.Add(new Modeler.Link() { Grade = 0, Sequence = new List<string>(new[] { "aas", "dfg", "ferr" }) });
				mod.Write(@"..\..\out.xml");
			}
			catch (Exception e)
			{
				string s = e.Message;
			}
		}

	}
}
