using System;
using System.IO;
using NonValidating;
using System.Xml;

public class test
{
	public static void Main(string[] args)
	{
		StreamReader inFile = new StreamReader(args[0]);
		string ret = inFile.ReadToEnd();
		inFile.Close();
		NonValidatingReader reader = new NonValidatingReader(ret);
		string top = "bz:bug";
		while (reader.Read()) 
		{
			if (reader.NodeType == XmlNodeType.Element) 
			{
				Console.WriteLine("some element: {0}",reader.Name);
				if (reader.Name.Equals(top)) 
				{
					reader.Read();
					Console.WriteLine("Equals");
					string element = null;
					while (!reader.Name.Equals(top))
					{
						switch(reader.NodeType)
						{
							case XmlNodeType.Element:
								element = reader.Name;
								Console.WriteLine("element: {0}",element);
								break;
							case XmlNodeType.Text:
								Console.WriteLine("text: {0}",reader.Value);
								break;
						}
						reader.Read();
					}
				}      
			}
		}
	}
}
