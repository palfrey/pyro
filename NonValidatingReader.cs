using System;
using System.Xml;
using System.Collections.Generic;

namespace NonValidating
{
	public class NonValidatingReader
	{
		private string content;
		private int index = 0;

		public XmlNodeType NodeType = XmlNodeType.None;
		public string Name = null;
		public string Value = null;
		public string Attributes = null;

		public class Names: List<string>
		{
			public new string Add(string input)
			{
				base.Add(input);
				return input;
			}
		}

		public Names NameTable;

		public NonValidatingReader(string content)
		{
			this.content = content;
			NameTable = new Names();
		}

		public void ReadOuterXml()
		{
			if (NodeType != XmlNodeType.Element)
				throw new Exception("Nodetype during readouterxml should be element");
			string find = Name;
			//Console.WriteLine("skipping until we see a {0}",find);
			while(Read())
			{
				if (Name.CompareTo(find)==0)
					break;
			}
		}

		public bool Read()
		{
			int angle;
			if (index == content.Length)
			{
				NodeType = XmlNodeType.None;
				return false;
			}
			switch(NodeType)
			{
				case XmlNodeType.None:
					angle = content.IndexOf("<",index);
					if (angle == -1)
						throw new Exception("no angle none");
					int endangle = content.IndexOf(">",angle);
					if (endangle == -1)
						throw new Exception("no endangle");
					Name = content.Substring(angle+1,endangle-angle-1);
					NodeType = XmlNodeType.Element;
					if (Name[0] == '/')
					{
						NodeType = XmlNodeType.EndElement;
						Name = Name.Substring(1);
					}
					if (Name.IndexOf(" ")!=-1)
					{
						Attributes = Name.Substring(Name.IndexOf(" ")+1);
						Name = Name.Substring(0,Name.IndexOf(" "));
					}
					index = endangle+1;
					//Console.WriteLine("name: {0}",Name);
					return true;
						
				case XmlNodeType.Element:
				case XmlNodeType.EndElement:
					angle = content.IndexOf("<",index);
					if (angle == -1)
						throw new Exception("no angle");
					string text = content.Substring(index,angle-index).Trim();
					if (text.Length>0)
					{
						index += text.Length;
						Value = text;
						NodeType = XmlNodeType.Text;
						//Console.WriteLine("value: {0}",Value);
						return true;
					}
					else
						goto case XmlNodeType.None;

				case XmlNodeType.Text:		
					goto case XmlNodeType.None;

				default:
					throw new Exception("bad type");
			}
			return false;
		}
	}
}
