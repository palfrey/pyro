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
			return ret;
		}

		public override string ReadLine()
		{
			string ret = base.ReadLine();
			Console.WriteLine("readline call: {0}",ret);
			throw new Exception();
			return ret;
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
			return ret;
		}

		private static void checkString(char[] buffer, int start, int count)
		{
			char[] valids = {'<','\n','>','/','!','\"','=',' ','?',':','.','-','_','(',')','@','&',';',',','#','*'};
			for (int i=start;i<start+count;i++)
			{
				if (!Char.IsLetterOrDigit(buffer[i]) && Array.IndexOf(valids,buffer[i])==-1)
				{
					buffer[i] = '?';
					if (i!=start && buffer[i-1]=='<')
						buffer[i-1] = '?';
				}
				if (i!=start)
				{
					if (buffer[i-1] == '<')
					{
						if (buffer[i] == '<' || (buffer[i]!='/' && (Char.ToLower(buffer[i])<'a' || Char.ToLower(buffer[i])>'z')))
							buffer[i-1] = '?';
					}
				}
			}
			/*if (ret!=0)
				Console.WriteLine(buffer,index,ret);*/
		}
	}
}
