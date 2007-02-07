using System;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;

namespace Pyro
{
	public class Bug
	{
		StringHash values = null;
		string[] comments = null;
		int _id;
		int dupid = -1;

		string _raw = null;

		string cachepath = "cache";

		public int id
		{
			get {
				return this._id;
			}
		}
		
		string getData(string url, string cache)
		{
			string path = Path.Combine(cachepath,cache);
			string ret = "";
			FileInfo fi = new FileInfo(path);
			if (!fi.Exists)
			{
				Console.WriteLine("grabbing {0}",url);
				HttpWebRequest wr = (HttpWebRequest) WebRequest.Create(url);
				HttpWebResponse wre = (HttpWebResponse) wr.GetResponse();
				StreamReader sr = new StreamReader(wre.GetResponseStream(), Encoding.ASCII);
				try
				{
					ret = sr.ReadToEnd();
				}
				catch
				{
					Console.WriteLine("Exception while reading bug");
				}
				sr.Close();
				if (!Directory.Exists(cachepath))
				{
					Directory.CreateDirectory(cachepath);
				}
				TextWriter outFile = new StreamWriter(path);
				outFile.Write(ret);
				outFile.Close();
			}
			else
			{
				TextReader inFile = new StreamReader(path);
				ret = inFile.ReadToEnd();
				inFile.Close();
			}
			return ret;
		}

		public string raw
		{
			get
			{
				if (this._raw == null)
				{
					this._raw = this.getData("http://bugzilla.gnome.org/show_bug.cgi?id="+this.id, String.Concat(this.id));
				}
				return this._raw;
			}
		}
		
		public Bug(int id)
		{
			this._id = id;
		}

		string[] getComments()
		{
			if (this.comments == null)
			{
				string pattern = "<pre id=\"comment_text_(\\d+)\">(.*?)</pre>";
				ArrayList ret = new ArrayList();
				foreach (Match m in Regex.Matches(this.raw, pattern, RegexOptions.Singleline))
				{
					//Console.WriteLine(m.Groups[0].Captures.Count);
					//Console.WriteLine(m.Groups[0].Captures[0].Value);
					ret.Add(m.Groups[0].Captures[0].Value);
				}
				//Console.WriteLine(ret.Count);
				this.comments = (string[])ret.ToArray(typeof(string));
			}
			return this.comments;
		}
		
		public class StringHash: Hashtable
		{
			public string this[string idx]
			{
				get
				{
					return base[idx].ToString();
				}
			}
		}
		
		
		public string this[string idx]
		{
			get
			{
				if (this.values==null || (this._raw == null && !this.values.ContainsKey(idx)))
				{
					string pattern = @"<td>\s+<b>([^<:]+):</b>\s+</td>\s+<td>(.*?)</td>";
					this.values = new StringHash();
					foreach (Match m in Regex.Matches(this.raw, pattern, RegexOptions.Singleline))
					{
						//Console.WriteLine(m.ToString());
						//Console.WriteLine(m.Groups[1].Captures[0].Value);
						string value = Bug.stripAll(m.Groups[2].Captures[0].Value);
						//Console.WriteLine(value);
						this.values.Add(m.Groups[1].Captures[0].Value.Trim(),value);
					}
				}
				return this.values[idx].ToString();
			}
		}
		
		static string stripAll(string inVal)
		{
			return Regex.Replace(inVal, @"</?(?i:a|script|embed|object|frameset|frame|iframe|meta|link|style|span)(.|\n)*?>", "").Trim();
		}

		public bool triageable()
		{
			if ((this["Status"]!="UNCONFIRMED" && this["Status"]!="NEEDINFO") || this["Severity"]!="critical" || this["Priority"]!="High")
				return false;
			return true;
		}
		
		public Stacktrace getStacktrace()
		{
			if (this.id == 0)
				throw new Exception();
			return new Stacktrace(this.getComments()[0]);
		}
		
		public Bug [] similar()
		{
			StringHash[] core = Bug.tableParser(this.getData("http://bugzilla.gnome.org/dupfinder/simple-dup-finder.cgi?id="+this.id, String.Concat(id)+"-dupe"));
			ArrayList bugs = new ArrayList();
			foreach(StringHash h in core)
			{
				Bug b = new Bug(System.Convert.ToInt32(h["Bug #"],10));
				b.values = new StringHash();
				assignKeys(b,h);
				bugs.Add(b);
			}
			return (Bug[])bugs.ToArray(typeof(Bug));
		}

		public Bug [] corebugs()
		{
			string corelist = this.getData("http://bugzilla.gnome.org/reports/core-bugs-today.cgi","corebugs");
			//Console.WriteLine(corelist);
			Match m = Regex.Match(corelist,"(http://bugzilla.gnome.org/buglist.cgi\\?bug_id=[^\"]+)");
			//Console.WriteLine(m.ToString());
			StringHash[] core = Bug.tableParser(this.getData(m.ToString(),"corebugs-real"));
			ArrayList bugs = new ArrayList();
			foreach(StringHash h in core)
			{
				Bug b = new Bug(System.Convert.ToInt32(h["ID"],10));
				b.values = new StringHash();
				assignKeys(b,h);
				bugs.Add(b);
			}
			return (Bug[])bugs.ToArray(typeof(Bug));
		}

		void assignKeys(Bug b,StringHash h)
		{
			foreach(string key in h.Keys)
			{
				switch(key)
				{
					case "Bug #":
					case "ID":
						break;
					case "Status":
					{
						switch(h[key])
						{
							case "RESO":
								b.values[key] = "RESOLVED";
								break;
							case "NEED":
								b.values[key] = "NEEDINFO";
								break;
							case "UNCO":
								b.values[key] = "UNCONFIRMED";
								break;
							default:
								throw new Exception(h[key]);
						}
						break;
					}
					case "Resolution":
					{
						if (h[key].Length>3 && h[key].Substring(0,4) == "DUPL")
						{
							b.values[key] = "DUPLICATE";
							int of = h[key].IndexOf("(of "); 
							if (of!=-1)
							{
								b.dupid = int.Parse(h[key].Substring(of+4,h[key].IndexOf(")",of)-of-4));
								//Console.WriteLine("dupid: {0} orig: {1}",b.dupid,h[key]);
							}
							break;	
						}
						switch(h[key])
						{
							case "FIXE":
								b.values[key] = "FIXED";
								break;
							case "INCO":
								b.values[key] = "INCOMPLETE";
								break;
							case "":
								break;
							case "NOTG":
								b.values[key] = "NOTGNOME";
								break;
							default:
								throw new Exception(h[key]);
						}
						break;
					}
					case "Sev":
					{
						switch(h[key])
						{
							case "cri":
								b.values["Severity"] = "critical";
								break;
							default:
								throw new Exception(h[key]);
						}
						break;
					}
					case "Pri":
					{
						switch(h[key])
						{
							case "Hig":
								b.values["Priority"] = "High";
								break;
							default:
								throw new Exception(h[key]);
						}
						break;
					}
					case "Summary":
					case "Product":
					case "OS":
						break;
					default:
						throw new Exception(key);
				}
			}
		}

		static StringHash[] tableParser(string input)
		{
			ArrayList rows = new ArrayList();
			Match tab = Regex.Match(input, "<table(.*?)</table>",RegexOptions.Singleline);
			if (!tab.Success)
				throw new Exception();
			string table = tab.ToString();
			string [] headers = null;
			ArrayList hdrs = new ArrayList();
			foreach (Match m in Regex.Matches(table, "<tr[^>]*>(.*?)</tr>", RegexOptions.Singleline))
			{
				string row = m.ToString();
				StringHash current = null;
				int index = 0;
				if (headers!=null)
					current = new StringHash();
				foreach (Match m2 in Regex.Matches(row, "<t(?:d|h)[^>]*>(.*?)</t(?:d|h)>", RegexOptions.Singleline))
				{
					string value = m2.Groups[1].Captures[0].Value;
					if (headers == null)
					{
						hdrs.Add(Bug.stripAll(value));
					}
					else
					{
						current.Add(headers[index],Bug.stripAll(value));
						index ++;
					}
				}
				if (headers == null)
				{
					headers = (string[])hdrs.ToArray(typeof(string));
					if (headers.Length == 0)
						throw new Exception();
					/*foreach(string s in headers)
					{
						Console.WriteLine("header: {0}",s);
					}*/
				}
				else
				{
					rows.Add(current);
					/*foreach(string key in current.Keys)
					{
						Console.WriteLine("row: {0} = {1}",key,current[key]);
					}*/
				}
			}
			return (StringHash[])rows.ToArray(typeof(StringHash));
		}
	}


	public class Stacktrace
	{
		const string pattern = "#(\\d+)\\s+(?:0x[\\da-f]+ in <span class=\"trace-function\">([^<]+)</span>\\s+\\([^\\)]*?\\)\\s+(?:at\\s+([^:]+:\\d+)|from\\s+([^ \n\r]+))?|<a name=\"stacktrace\"></a><span class=\"trace-handler\">&lt;(signal handler) called&gt;</span>)"; //(?:(?)|
		
		string raw = "";
		ArrayList content = null;
		
		public Stacktrace(string data)
		{
			this.raw = data;
			int limit = 0;
			this.content = new ArrayList();
			bool seen_signal = false;
			int idx = -1;
			foreach (Match m in Regex.Matches(this.raw, Stacktrace.pattern, RegexOptions.Singleline))
			{
				int new_idx = System.Convert.ToInt32(m.Groups[1].Captures[0].Value, 10);
				if (idx!=-1 && new_idx<idx)
				{
					if(seen_signal)
						break;
					this.content = new ArrayList();
					limit = 0;
				}
				idx = new_idx;	
				if (m.Groups[2].Captures.Count!=0)
				{
					string[] tostore = new string[2];
					tostore[0] = m.Groups[2].Captures[0].Value;
					if (m.Groups[3].Captures.Count!=0)
						tostore[1] = m.Groups[3].Captures[0].Value;
					else if (m.Groups[4].Captures.Count!=0)
						tostore[1] = m.Groups[4].Captures[0].Value;
					else
						tostore[1] = "";
					if (seen_signal)
					{
						this.content.Add(tostore);
						limit++;
						if (limit==6)
							break;
					}
				}
				else if (m.Groups[5].Captures.Count!=0)
				{
					//Console.WriteLine(" signal handler!");
					seen_signal = true;
				}
			}
			/*foreach(string[] s in this.content.ToArray(typeof(string[])))
			{
				Console.WriteLine(s[0]);
			}*/
		}

		public static bool operator == (Stacktrace left, Stacktrace right)
		{
			return left.Equals(right);
		}
		
		public static bool operator != (Stacktrace left, Stacktrace right)
		{
			return !left.Equals(right);
		}

		public override bool Equals(Object right)
		{
			if (right.GetType()!=this.GetType())
				return false;
			return this.Equals((Stacktrace)right);
		}

		public bool Equals(Stacktrace right)
		{
			if (ReferenceEquals(right,null))
				return false;
			if (ReferenceEquals(this,right))
				return true;
			if (this.content.Count!= right.content.Count)
				return false;
			for(int idx = 0;idx<this.content.Count;idx++)
			{
				string [] one = (string[])this.content[idx];
				string [] two = (string[])right.content[idx];
				for(int j=0;j<2;j++)
				{
					//Console.WriteLine("comparing {0} and {1}",one[j],two[j]);
					if (one[j]!=two[j])
						return false;
				}
			}
			return true;
		}

		public override int GetHashCode()
		{
			return this.content.GetHashCode();
		}

		public bool usable()
		{
			if (this.content.Count == 0)
				return false;
			return true;
		}
	}
}
