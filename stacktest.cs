using Pyro;
using System.IO;

public class StackTest
{
	public static void Main(string[] args)
	{
		StreamReader inFile = new StreamReader(args[0]);
		string xsl = inFile.ReadToEnd();
		inFile.Close();
		Stacktrace st = new Stacktrace(0,xsl);
	}
}
