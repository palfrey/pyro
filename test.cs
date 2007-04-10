using System;
using System.IO;
using NonValidating;
using System.Xml;
using System.Xml.Xsl;
using System.Collections.Generic;

public class test
{
	public class StringHash: Dictionary <string,string> /*Hashtable*/
	{
	}

	public static StringHash[] xmlParser(string input, string separator, StringHash mappings)
	{
		List<StringHash> rows = new List<StringHash>();
		//XmlTextReader reader = new XmlTextReader(new SafeStringReader(input));
		NonValidatingReader reader = new NonValidatingReader(input);
		string top = reader.NameTable.Add(separator);
		while (reader.Read()) 
		{
			if (reader.NodeType == XmlNodeType.Element && reader.Name.CompareTo(top)==0) 
			{
				StringHash ret = new StringHash();
				reader.Read();
				string element = null;
				while (reader.Name.CompareTo(top)!=0)
				{
					switch(reader.NodeType)
					{
						case XmlNodeType.Element:
							element = reader.Name;
							if (element!=null && mappings.ContainsKey(element))
							{
								if (mappings[element] == null) /* therefore, skip */
									reader.ReadOuterXml();	
								else
									element = mappings[element];
							}
							break;
						case XmlNodeType.Text:
							if (!ret.ContainsKey(element))
								ret.Add(element,reader.Value);
							break;
					}
					if (reader.Read()==false)
						break;
				}
				rows.Add(ret);
				continue;
			}
		}
		return rows.ToArray();
	}

	private static void parseSearchResults(string curr)
	{
		StringHash mappings = null;
		if (mappings == null)
		{
			mappings = new StringHash();
			mappings.Add("bz:id","ID");
		}
		StringHash[] core = xmlParser(curr,"bz:bug",mappings);
	}

	public static void Main(string[] args)
	{
		StreamReader inFile = new StreamReader(args[0]);
		string xsl = inFile.ReadToEnd();
		inFile.Close();
		parseSearchResults(xsl);
	}
}
