using System;
using System.IO;

namespace Pyro
{
	public class SafeStringReader: StringReader
	{
		public SafeStringReader(string s):base(s){}
		public override int Read()
		{
			int ret = base.Read();
			Console.WriteLine("read call: {0}",ret);
			throw new Exception();
			//return ret;
		}

		public override string ReadLine()
		{
			string ret = base.ReadLine();
			Console.WriteLine("readline call: {0}",ret);
			throw new Exception();
			//return ret;
		}

		public override string ReadToEnd()
		{
			string ret = base.ReadToEnd();
			//Console.WriteLine("readtoend call: {0}",ret);
			char[] data = ret.ToCharArray();
			checkString(data,0,ret.Length);
			return new String(data);
		}

		public override int Read (char[] buffer, int index, int count)
		{
			int ret = base.Read(buffer,index,count);
			Console.WriteLine("complex read call: {0} {1}",ret,buffer);
			checkString(buffer,index,ret);
			return ret;
		}

		public override int ReadBlock (char[] buffer, int index, int count)
		{
			int ret = base.ReadBlock(buffer,index,count);
			Console.WriteLine("readblock call: {0}",ret);
			throw new Exception();
			//return ret;
		}

		private static char[] valids = {'<','\n','>','/','!','\"','=',' ','?',':','.','-','_','(',')','@','&',';',',','#','*','[',']'};
		private static string[] entities = {"nbsp","apos","quot","gt","lt","amp"};

		private static void checkString(char[] buffer, int start, int count)
		{
			int inEnt = -1;
			for (int i=start;i<start+count;i++)
			{
				if (!Char.IsLetterOrDigit(buffer[i]) && Array.IndexOf(valids,buffer[i])==-1)
				{
					buffer[i] = '?';
					if (i!=start && buffer[i-1]=='<')
						buffer[i-1] = '?';
				}
				else if (i!=start)
				{
					if (buffer[i-1] == '<')
					{
						if (buffer[i] == '<' || (buffer[i]!='/' && (Char.ToLower(buffer[i])<'a' || Char.ToLower(buffer[i])>'z')))
							buffer[i-1] = '?';
					}
				}
				if (inEnt==-1 && buffer[i]=='&')
					inEnt = i+1;
				else if (inEnt!=-1)
				{
					if (buffer[i] == ';')
					{
						string check = new String(buffer).Substring(inEnt,i-inEnt);
						if (Array.IndexOf(entities,check)==-1)
						{
							Console.WriteLine("{0} isn't a valid entity",check);
							buffer[i] = '?';
							buffer[inEnt-1] = '?';
						}
						inEnt = -1;	
					}
					else if (Char.ToLower(buffer[i])<'a' || Char.ToLower(buffer[i])>'z')
						inEnt = -1;
				}
			}
			/*if (ret!=0)
				Console.WriteLine(buffer,index,ret);*/
		}
	}
}
