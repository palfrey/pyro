using Pyro;
using System.IO;

public class StackTest
{
	public static void Main(string[] args)
	{
		SafeStreamReader inFile = new SafeStreamReader(args[0], System.Text.Encoding.ASCII);
		string xsl = inFile.ReadToEnd();
		inFile.Close();
		Stacktrace st = new Stacktrace(0,xsl);
		st.print();
	}
}
