using System;
using System.Xml;
using System.Collections.Generic;

namespace NonValidating
{
	public class NonValidatingReader: XmlReader
	{
		private string content;
		private int index = 0,begin=0;

		public new XmlNodeType NodeType = XmlNodeType.None;
		public new string Name = null;
		public new string Value = null;
		public string Attributes = null;

		/*public class Names: List<string>
		{
			public new string Add(string input)
			{
				base.Add(input);
				return input;
			}
		}

		public Names NameTable;*/

		public NonValidatingReader(string content):base()
		{
			this.content = content;
			//NameTable = new Names();
		}

		public override string ReadOuterXml()
		{
			if (NodeType != XmlNodeType.Element)
				throw new Exception("Nodetype during readouterxml should be element");
			string find = Name;
			int start = begin;
			Console.WriteLine("skipping until we see a {0}",find);
			while(Read())
			{
				if (NodeType == XmlNodeType.EndElement && Name.CompareTo(find)==0)
					break;
			}
			return content.Substring(start,index-start);

		}

		public override bool Read()
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
					begin = index;
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
						begin = index;
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
		}
	}
}
