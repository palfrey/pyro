using System;
using System.Xml;
using System.Collections.Generic;
using System.Text;

namespace NonValidating
{
	public class NonValidatingReader: XmlReader
	{
		private string content = String.Empty;
		private int index = 0,begin=0;

		private XmlNodeType _NodeType = XmlNodeType.None;
		public override XmlNodeType NodeType
		{
			get {
				return _NodeType;
			}
		}
		
		private string _Name = String.Empty;
		public override string Name
		{
			get {
				if (NodeType == XmlNodeType.Element || NodeType == XmlNodeType.EndElement)
				{
					//Console.WriteLine("Looking at name '{0}'",_Name);
					return _Name;
				}
				else if (NodeType == XmlNodeType.Attribute)
				{
					string p = Prefix;
					//Console.WriteLine("Grabbing {0} ({1})",GetAttributeName(attr_index),_Attributes[GetAttributeName(attr_index)]);
					if (p == String.Empty)
						return GetAttributeName(attr_index);
					else	
						return p+":"+GetAttributeName(attr_index);
				}
				else
					return String.Empty;
			}
		}

		private string _Value = String.Empty;
		public override string Value
		{
			get {
				if (NodeType == XmlNodeType.Text)
					return _Value;
				else if (NodeType == XmlNodeType.Attribute)
					return GetAttribute(attr_index);
				else
					return String.Empty;
			}
		}

		public override string BaseURI
		{
			get {
				return String.Empty; /* FIXME: other values ?*/
			}
		}

		private int _Depth = 0;
		public override int Depth
		{
			get {
				return _Depth;
			}
		}

		private ReadState rs = ReadState.Initial;
		public override ReadState ReadState
		{
			get {
				return rs;
			}
		}

		public override bool EOF
		{
			get {
				return rs == ReadState.EndOfFile;
			}
		}

		public override bool HasValue
		{
			get {
				return NodeType == XmlNodeType.Text || NodeType == XmlNodeType.Attribute;
			}
		}

		private bool isEmpty = false;
		public override bool IsEmptyElement
		{
			get {
				return isEmpty;
			}
		}

		public override string LocalName
		{
			get {
				if (NodeType == XmlNodeType.Text)
					return String.Empty;
				string N = Name;
				int idx = N.IndexOf(":");
				if (idx!=-1)
					return N.Substring(idx+1);
				else
					return N;
			}
		}

		public override string NamespaceURI
		{
			get {
				string N = Name;
				try
				{
					if (N.IndexOf(":")!=-1)
						return Namespaces[NameTable.Add(N.Substring(0,N.IndexOf(":")))];
					/*else if (NodeType == XmlNodeType.Attribute && _Name.IndexOf(":")!=-1)
						return Namespaces[NameTable.Add(_Name.Substring(0,_Name.IndexOf(":")))];*/
					else if (NodeType == XmlNodeType.Attribute)
						return "";
					else	
						return Namespaces[String.Empty];
				}
				catch(KeyNotFoundException e)
				{
					if (N.IndexOf(":")!=-1 && N.IndexOf("xmlns:")==-1)
					{
						Console.WriteLine("name: {0} ns: {1}",N,N.Substring(0,N.IndexOf(":")));
						throw e;
					}
					return String.Empty;
				}
			}
		}

		private Dictionary<string,string> _Attributes = null;
		private int attr_index = 0;

		public override int AttributeCount
		{
			get {
				if (NodeType!=XmlNodeType.Element && NodeType!=XmlNodeType.Attribute)
					return 0;
				else if (_Attributes!=null)
					return _Attributes.Count;
				else
					return 0;
			}
		}

		public override string GetAttribute(string s)
		{
			throw new Exception(s);
			return _Attributes[s];
		}
		
		public override string GetAttribute(string s, string ns)
		{
			throw new Exception();
			return _Attributes[ns+":"+s]; // FIXME: not correct
		}

		private Dictionary<string,string> Namespaces;
		
		public override string LookupNamespace(string s)
		{
			return Namespaces[s];
		}

		private string GetAttributeName(int i)
		{
			string[] vals = new string[_Attributes.Keys.Count];
			_Attributes.Keys.CopyTo(vals,0);
			return vals[i];
		}

		public override string GetAttribute(int i)
		{
			//Console.WriteLine("Grabbing {0} of {2} ({1})",GetAttributeName(i),_Attributes[GetAttributeName(i)],NamespaceURI);
			return _Attributes[GetAttributeName(i)];
		}

		public override bool MoveToAttribute(string s, string ns)
		{
			throw new Exception();
			return false; // FIXME: not correct!
		}

		public override bool MoveToAttribute(string s)
		{
			throw new Exception();
			return false; // FIXME: not correct!
		}

		public override void MoveToAttribute(int i) // FIXME: does nothing!
		{ 
			throw new Exception();
		}

		public override bool MoveToElement() 
		{
			if (NodeType == XmlNodeType.Attribute)
			{
				_NodeType = XmlNodeType.Element;
				return true;
			}
			else
			{
				throw new Exception();
				return false;
			}
		}

		public override bool MoveToFirstAttribute()
		{
			if (NodeType == XmlNodeType.Attribute || (NodeType == XmlNodeType.Element && _Attributes.Count>0))
			{
				_NodeType = XmlNodeType.Attribute;
				attr_index = 0;
				return true;
			}
			else
			{
				//Console.WriteLine("nodetype: {0}, Name:{1}",NodeType,_Name);
				//throw new Exception();
				return false;
			}
		}

		public override bool MoveToNextAttribute()
		{
			if (NodeType == XmlNodeType.Element && _Attributes.Count>0)
				return MoveToFirstAttribute();
			else if (NodeType == XmlNodeType.Attribute &&_Attributes.Count>attr_index+1)
			{
				attr_index+=1;
				return true;
			}
			else
			{
				//throw new Exception();
				return false;
			}
		}

		public override bool ReadAttributeValue() // FIXME: does nothing!
		{
			throw new Exception();
			return false;
		}

		public override void ResolveEntity() // FIXME: does nothing!
		{
			throw new Exception();
		}

		private NameTable _NameTable;
		public override XmlNameTable NameTable
		{
			get {
				return _NameTable;
			}
		}

		public override string Prefix
		{
			get {
				if (NodeType == XmlNodeType.Attribute || _Name.IndexOf(":")==-1)
					return String.Empty;
				else
					return NameTable.Add(_Name.Substring(0,_Name.IndexOf(':')));
			}
		}

		public override XmlSpace XmlSpace
		{
			get {
				return XmlSpace.Preserve;
			}
		}

		public NonValidatingReader(string content):base()
		{
			this.content = content;
			_NameTable = new NameTable();
			Namespaces = new Dictionary<string,string>();
			Namespaces.Add(_NameTable.Add("xml"),"http://www.w3.org/XML/1998/namespace");
		}

		public override string ReadOuterXml()
		{
			if (_NodeType != XmlNodeType.Element)
				throw new Exception("Nodetype during readouterxml should be element");
			string find = _Name;
			int start = begin;
			Console.WriteLine("skipping until we see a {0}",find);
			while(Read())
			{
				if (_NodeType == XmlNodeType.EndElement && _Name.CompareTo(find)==0)
					break;
				//Console.WriteLine("current item is {0} of type {1}",Name,NodeType);	
			}
			Console.WriteLine("skipped until we saw a {0}",find);
			//Read();
			Console.WriteLine("current item is {0} of type {1}",_Name,NodeType);	
			return content.Substring(start,index-start);
		}

		public override bool Read()
		{
			int angle;
			if (index == content.Length || content.Substring(index).Trim().Length==0)
			{
				_NodeType = XmlNodeType.None;
				rs = ReadState.EndOfFile;
				return false;
			}
			rs = ReadState.Interactive;
			switch(NodeType)
			{
				case XmlNodeType.Text:		
				case XmlNodeType.None:
					StringBuilder tempName = null;
					angle = content.IndexOf("<",index);
					if (angle == -1)
					{
						Console.WriteLine("Content: {0}",content.Substring(index));
						throw new Exception("no angle none");
					}
					int endangle = content.IndexOf(">",angle);
					if (endangle == -1)
						throw new Exception("no endangle");
					tempName = new StringBuilder(content.Substring(angle+1,endangle-angle-1));
					//Console.WriteLine("name: {0}",tempName);
					_NodeType = XmlNodeType.Element;
					isEmpty = false;
					if (tempName[0] == '/')
					{
						_NodeType = XmlNodeType.EndElement;
						tempName.Remove(0,1);
						_Depth -=1;
					}
					else
					{
						if (tempName[tempName.Length-1] == '/')
						{
							isEmpty = true;
							tempName.Length-=1;
						}
					}
					if (tempName.ToString().IndexOf("?xml")==0)
					{
						_NodeType = XmlNodeType.XmlDeclaration;
						tempName.Length-=1;
					}
					int space = tempName.ToString().IndexOf(" ");
					if (space!=-1)
					{
						string raw = tempName.ToString(space+1,tempName.Length-space-1);
						_Attributes = new Dictionary<string,string>();
						foreach (String s in raw.Split(new char[]{' ','\t','\n'},StringSplitOptions.RemoveEmptyEntries))
						{
							string[] bits = s.Trim().Split('=');
							if (bits.Length!=2)
							{
								/*Console.WriteLine("raw: {0}",raw);
								Console.WriteLine("orig: '{0}'",s);
								Console.WriteLine("bits: {0}",bits.Length);
								for(int i=0;i<bits.Length;i++)
									Console.WriteLine("bits[{0}] '{1}'",i,bits[i]);*/
								continue; // ignore!
								//throw new Exception("bits length is weird");
							}
							try
							{
								if (bits[1][0]=='\"' || bits[1][0] == '\'')
									bits[1] = bits[1].Substring(1);
								if (bits[1][bits[1].Length-1]=='\"' || bits[1][bits[1].Length-1] == '\'')
									bits[1] = bits[1].Substring(0,bits[1].Length-1);
							}
							catch(Exception e)
							{
								Console.WriteLine("raw: {0}",raw);
								Console.WriteLine("orig: {0}",s);
								Console.WriteLine("bits: {0}",bits.Length);
								for(int i=0;i<bits.Length;i++)
									Console.WriteLine("bits[{0}] {1}",i,bits[i]);
								throw e;
							}
							//Console.WriteLine("new attr: {0}={1}",bits[0],bits[1]);
							if (bits[0].IndexOf("xmlns")!=-1) // new namespace
							{
								if (bits[0].CompareTo("xmlns")==0)
								{
									Namespaces.Add(String.Empty,NameTable.Add(bits[1]));
									Console.WriteLine("default ns: {0}",Namespaces[String.Empty]);
								}
								else	
								{
									Namespaces.Add(NameTable.Add(bits[0].Substring(bits[0].IndexOf(":")+1)),NameTable.Add(bits[1]));
									Console.WriteLine("new ns: {0}",bits[1]);
								}
							}
							if (!_Attributes.ContainsKey(NameTable.Add(bits[0])))
								_Attributes.Add(NameTable.Add(bits[0]),NameTable.Add(bits[1]));
						}
						tempName.Remove(space,tempName.Length-space);
					}
					//Console.WriteLine("name: {0}",tempName);
					_Name = NameTable.Add(tempName.ToString());
					_Value = String.Empty;
					begin = index;
					index = endangle+1;
					break;
						
				case XmlNodeType.Element:
					if (_Attributes!=null && _Attributes.Count>attr_index)
					{
						_NodeType = XmlNodeType.Attribute;
						attr_index = 0;
						break;
					}
					else
						goto case XmlNodeType.EndElement;

				case XmlNodeType.EndElement:
				case XmlNodeType.XmlDeclaration:
					angle = content.IndexOf("<",index);
					if (angle == -1)
					{
						Console.WriteLine("Content: {0}",content.Substring(index));
						throw new Exception("no angle");
					}
					if (angle>index)
					{
						string text = content.Substring(index,angle-index);
						if (text.Trim().Length>0)
						{
							begin = index;
							index += text.Length;
							_Value = text;
							_NodeType = XmlNodeType.Text;
							//Console.WriteLine("text node value: '{0}'",_Value);
							break;
						}
					}
					goto case XmlNodeType.None;

				case XmlNodeType.Attribute:
					if (_Attributes.Count>attr_index+1)
					{
						attr_index+=1;
						break;
					}
					else
						goto case XmlNodeType.EndElement;

				default:
					throw new Exception("bad type");
			}
			return true;
		}
		
		public override void Close()
		{
			content = String.Empty;
			rs = ReadState.Closed;
		}
	}
}
