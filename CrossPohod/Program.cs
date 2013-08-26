using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;

using Distance = System.Collections.Generic.Dictionary<string, CrossPohod.Node>;

namespace CrossPohod
{

	public class CPException : Exception 
	{
		public CPException() : base() { }
		public CPException(string message) : base(message) { }
		public CPException(string message, Exception e) : base(message, e) { }
		public CPException(string format, params Object[] args) : base(String.Format(format, args)) {}
	}

	public class PrintSyntaxException : Exception
	{
		public PrintSyntaxException() : base() { }
	}

	public class ProgramParams : ModelerParams
	{
		public string Out = "out.csv";

		public int Times = 1;
		public TimeSpan Start = TimeSpan.MinValue;
		public bool SmartStart = false;


		public static ProgramParams Parse(string[] args)
		{
			if (args == null || args.Length < 2)
				throw new CPException("Недостаточно параметров");

			ProgramParams res = new ProgramParams()
			{
				Times = 1,
				Unlimited = false,
				Start = new TimeSpan(6, 0, 0),

				Source = args[0],
				Out = args[1]
			};


			if (args.Length < 3)
				return res;

			int i = 2;
			string arg;

			while (i < args.Length)
			{
				arg = args[i++].ToLower();

				if ("-u" == arg || "-unlim" == arg)
					res.Unlimited = true;
				else if ("-sm" == arg || "-smart" == arg)
					res.SmartStart = true;
				else if ("-l" == arg || "-level" == arg)
				{
					if (i >= args.Length)
						throw NoValue(arg);
					arg = args[i++];

					double l;
					if (!double.TryParse(arg, out l))
						throw new CPException("Не могу прочесть уровень квантилей: '{0}'", arg);

					if (l <= 0 || l > 1)
						throw new CPException("Уровень квантилей вне диапазона (0; 1]", l);
					res.Level = l;
				}
				else if ("-t" == arg || "-transit" == arg)
				{
					res.Before = new TimeSpan(0, 3, 0);
					res.After = new TimeSpan(0, 5, 0);
					throw new CPException("Ключ -t временно не поддерживается");
				}
				else if ("-s" == arg || "-start" == arg)
				{
					if (i >= args.Length)
						throw NoValue(arg);
					arg = args[i++];

					if (!TimeSpan.TryParse(arg, out res.Start))
						throw new CPException("Не могу прочесть время старта: '{0}'", arg);
				}
				else if ("-h" == arg || "-help" == arg)
					throw new PrintSyntaxException();
				else if (arg.StartsWith("-"))
					throw new CPException("Неизвестный ключ: '{0}'", arg);
				else if (i == 3)	// не ключ!
				{
					if (!Int32.TryParse(arg, out res.Times) || res.Times <= 0)
						throw new CPException("Не могу прочесть число повторов: '{0}'", arg);
				}
			}

			return res;
		}

		private static CPException NoValue(string arg)
		{
			return new CPException("Нет значения для флага {0}", arg);
		}
	}

	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				Random r = new Random();

				ProgramParams param = ProgramParams.Parse(args);
				Modeler mod = Modeler.Read(param);

				FileStream fs = new FileStream(param.Out, FileMode.Create, FileAccess.Write);
				StreamWriter sw = new StreamWriter(fs);


				for (int i = 0; i < param.Times; i++)
				{
					if (i % 10 == 9)
						Console.WriteLine(i + 1);

					Program.StartDay1(r, mod.Nodes["Старт1"] as StartNode, mod.Teams, DateTime.MinValue + param.Start);
					mod.Model(r);
					mod.RetrieveTeamStat(0);

					Program.StartDay2(r, mod.Nodes["Старт2"] as StartNode, mod.Teams, DateTime.MinValue.AddDays(1.25), param.SmartStart);
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

				mod.PhaseStats(sw, param.Times);
				sw.Flush();
				fs.Close();
			}
			catch (CPException cp)
			{
				Console.WriteLine("Error:\n{0}\n", cp.Message);
				PrintSyntax();
			}
			catch (PrintSyntaxException)
			{
				PrintSyntax();
			}
			catch (Exception e)
			{
				Console.WriteLine("Error:\n{0}\n {1}\n", e.Message, e.StackTrace);
			}

			Console.Write("Press any key...");
			Console.ReadKey();
		}

		public static void PrintSyntax()
		{
			Console.WriteLine("Syntax:");
			Console.WriteLine("crosspohod config.xml out.csv [NNN] [-unlim] [-s h:mm] [-l lev] [-t] [-sm]");
			Console.WriteLine("\tNNN\tчисло повторений, значение по умолчанию 1");
			Console.WriteLine("\nSwitches:");
			Console.WriteLine("\tl level\tуровень квантилей. Значение по умолчанию 0,95. Например: -l 0,9");
			Console.WriteLine("\ts start\tначало работы старта 1 дня. Например: -s 6:20");
			Console.WriteLine("\tsm smart\tхитрый старт второго дня");
			Console.WriteLine("\tt transit\tдобавлять время на подготовку и сборы до и после тех. этапов");
			Console.WriteLine("\tu unlim\tрежим оценки необходимой пропускной способности");
		}

		private static List<Team> JoinLists(List<Team> a, List<Team>b)
		{
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

			return join;
		}

		public static void StartDay1(Random r, StartNode n, List<Team> teams, DateTime start)
		{
            if (n == null)
                throw new CPException("Не задан стартовый этап второго дня (Старт2)");

			List<Team> a = teams.Where(t => t.Grade >= 2)
								.OrderBy(t => r.Next())			// shuffle!
								.ToList();
			List<Team> b = teams.Where(t => t.Grade < 2)
								.OrderBy(t => r.Next())
								.ToList();

			int i = 0;
			int channels = n.Channels > 0 ? n.Channels : 1;
			foreach (var t in JoinLists(a, b))
			{
				n.AddToStart(r, t, start);
				t.SetStart(start);

				i++;
				if (i % channels == 0)
					start += n.Times.Max;
			}

		}

        public static void StartDay2(Random r, StartNode n, List<Team> teams, DateTime start, bool smart)
		{
            if (n == null)
                throw new CPException("Не задан стартовый этап второго дня (Старт2)");

			int APlus = 2;
			List<Team> a = teams.Where(t => t.Grade >= APlus)
								.OrderBy(t => t.GetStat(0).Work.TotalSeconds)
								.ToList();
			List<Team> b = teams.Where(t => t.Grade < APlus)
								.OrderBy(t => t.GetStat(0).Work.TotalSeconds)
								.ToList();

			int i = 0;
			int channels = n.Channels > 0 ? n.Channels : 1;
			foreach (var t in JoinLists(a, b))
			{
				n.AddToStart(r, t, start);
				t.SetStart(start);

				i++;
				start = Start(smart, start, i, channels, n.Times.Max);
			}
		}

		private static DateTime Start(bool smart, DateTime current, int idx, int channels, TimeSpan shift)
		{
			TimeSpan sh = TimeSpan.Zero;

			if (smart)
			{
				int quant = 3;
				if (idx > (quant-1) *channels && idx % channels == 0)
				{
					sh = shift;
					if (idx <= quant * channels)
						sh += new TimeSpan((quant-1)*shift.Ticks);
				}
			}
			else if (idx % channels == 0)
				sh = shift;

			return current + sh;
		}

        public static void AddTeam(Random r, StartNode n, List<Team> teams, DateTime t)
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
