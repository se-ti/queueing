﻿using System;
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
		public bool SmartStart = false;


		public static ProgramParams Parse(string[] args)
		{
			if (args == null || args.Length < 2)
				throw new CPException("Недостаточно параметров");

			ProgramParams res = new ProgramParams()
			{
				Times = 1,
				Unlimited = false,

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
                else if ("-i" == arg || "-ignoreTransit" == arg)
                    res.IgnoreTransit = true;
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

				var bom = new byte[] { 0xEF, 0xBB, 0xBF };
				sw.BaseStream.Write(bom, 0, bom.Length);

                int quant = 25;
				for (int i = 0; i < param.Times; i++)
				{
					if (i % quant == quant-1)
						Console.WriteLine(i + 1);


					Program.StartDay1(r, mod.Nodes["Старт1"] as StartNode, mod.Teams.Where(t => t.Grade >= 2).ToList(), new[] { 4 } );
					Program.StartDay1(r, mod.Nodes["Старт1Б"] as StartNode, mod.Teams.Where(t => t.Grade < 2).ToList());


					mod.Model(r);
					mod.RetrieveTeamStat(0);

					var days = 1;
					var start2 = "Старт2";
					if (mod.Nodes.ContainsKey(start2))
					{
						days++;
						Program.StartDay2(r, mod.Nodes[start2] as StartNode, mod.Teams, DateTime.MinValue.AddDays(1.25), param.SmartStart);
						mod.Model(r);
						mod.RetrieveTeamStat(1);
					}


					sw.WriteLine(Team.PrintHeader(days));
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
				Environment.ExitCode = 2;
			}
			catch (PrintSyntaxException)
			{
				PrintSyntax();
				Environment.ExitCode = 1;
			}
			catch (Exception e)
			{
				Console.WriteLine("Error:\n{0}\n {1}\n", e.Message, e.StackTrace);
				Environment.ExitCode = 3;
			}

			Console.Write("Press any key...");
			Console.ReadKey();
		}

		public static void PrintSyntax()
		{
			Console.WriteLine("Syntax:");
			Console.WriteLine("crosspohod config.xml out.csv [NNN] [-unlim] [-l lev] [-i] [-sm]");
			Console.WriteLine("\tNNN\tчисло повторений, значение по умолчанию 1");
			Console.WriteLine("\nSwitches:");
            Console.WriteLine("\ti ignoreTransit\tне учитывать время на подготовку и сборы до и после тех. этапов");
			Console.WriteLine("\tl level\tуровень квантилей. Значение по умолчанию 0,95. Например: -l 0,9");
			Console.WriteLine("\tsm smart\tхитрый старт второго дня");			
			Console.WriteLine("\tu unlim\tрежим оценки необходимой пропускной способности");
		}

		private static IEnumerable<Team> JoinLists(IEnumerable<Team> a, IEnumerable<Team>b)
		{
			bool hasA = true;
			bool hasB = true;
			var enA = a.GetEnumerator();
			var enB = b.GetEnumerator();
			do
			{
				hasA = hasA && enA.MoveNext();
				if (hasA)
					yield return enA.Current;
				hasB = hasB && enB.MoveNext();
				if (hasB)
					yield return enB.Current;
			}
			while (hasA || hasB);
		}

		public static void StartDay1(Random r, StartNode n, List<Team> teams, int[] groups = null)
		{
            if (n == null)
                throw new CPException("Не задан стартовый этап второго дня (Старт2)");

			var a = teams.Where(t => t.Grade >= 2)
								.OrderBy(t => r.Next());			// shuffle!
			var b = teams.Where(t => t.Grade < 2)
								.OrderBy(t => r.Next());

			DateTime start = DateTime.MinValue + n.Times.Open;
			int i = 0;
			int effChannels;
			int channels = Math.Max(n.Channels, 1);
			var grpIdx = 0;
			groups = groups ?? new int[] { };        // first start groups can be larger than n.Channels, to fill next stages faster
			foreach (var t in JoinLists(a, b))
			{
				n.AddTeam(r, t, start);
				t.SetStart(start);

				i++;
				effChannels = grpIdx < groups.Length ? groups[grpIdx] : channels;
				if (i % effChannels == 0)
				{
					start += n.Times.Max;
					i = 0;
					grpIdx++;
				}
			}
		}

        public static void StartDay2(Random r, StartNode n, List<Team> teams, DateTime start, bool smart)
		{
            if (n == null)
                throw new CPException("Не задан стартовый этап второго дня (Старт2)");

			int APlus = 2;
			var a = teams.Where(t => t.Grade >= APlus)
								.OrderBy(t => t.GetStat(0).Work.TotalSeconds);
			var b = teams.Where(t => t.Grade < APlus)
								.OrderBy(t => t.GetStat(0).Work.TotalSeconds);

			int i = 0;
			int channels = Math.Max(n.Channels, 1);
			foreach (var t in JoinLists(a, b))
			{
				n.AddTeam(r, t, start);
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
			n.AddTeam(r, team, t);
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
