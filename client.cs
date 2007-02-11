using System;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace Pyro
{
	public class Bug
	{
		StringHash values = null;
		string[] comments = null;
		int _id;
		int dupid = -1;

		string _raw = null;
		Bugzilla bugz = null;

		public int id
		{
			get {
				return this._id;
			}
		}
		
		public string raw
		{
			get
			{
				if (this._raw == null)
				{
					this._raw = bugz.getBug(this.id);
				}
				return this._raw;
			}
		}

		public void refresh()
		{
			this._raw = bugz.getBug(this.id,true);
		}
		
		public Bug(int id, Bugzilla bugz)
		{
			this._id = id;
			this.bugz = bugz;
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
			StringHash[] core = Bug.tableParser(bugz.simpleDupe(this.id));
			ArrayList bugs = new ArrayList();
			foreach(StringHash h in core)
			{
				Bug b = new Bug(System.Convert.ToInt32(h["Bug #"],10),this.bugz);
				b.values = new StringHash();
				assignKeys(b,h);
				bugs.Add(b);
			}
			return (Bug[])bugs.ToArray(typeof(Bug));
		}

		public Bug [] corebugs()
		{
			StringHash[] core = Bug.tableParser(bugz.corebugs());
			ArrayList bugs = new ArrayList();
			foreach(StringHash h in core)
			{
				Bug b = new Bug(System.Convert.ToInt32(h["ID"],10),this.bugz);
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

		public void setBadStacktrace()
		{
			refresh();
			if (this["Status"]!="NEEDINFO")
			{
				StringHash orig = parseInput();
				orig["comment"] = @"Thanks for taking the time to report this bug.
Unfortunately, that stack trace is missing some elements that will help a lot
to solve the problem, so it will be hard for the developers to fix that crash.
Can you get us a stack trace with debugging symbols? Please see
http://live.gnome.org/GettingTraces for more information on how to do so.
Thanks in advance!";
				orig["knob"] = "needinfo";
				bugz.changeBug(orig);
			}
			else
				Console.WriteLine("already marked as needinfo");
		}
		
		private StringHash parseInput()
		{
			StringHash ret = new StringHash();
			Match form = Regex.Match(this.raw, "<form name=\"changeform\" method=\"post\" action=\"process_bug.cgi\">(.*?)</form>",RegexOptions.Singleline);
			if (!form.Success)
				throw new Exception();
			foreach (Match m2 in Regex.Matches(form.ToString(), "<input([^>]*)>|<textarea([^>]*)>", RegexOptions.Singleline))
			{
				StringHash sh = new StringHash();
				foreach (Match p in Regex.Matches(m2.ToString(), "\\s+(.*?)=\"(.*?)\"", RegexOptions.Singleline))
				{
					sh.Add(p.Groups[1].Captures[0].Value,p.Groups[2].Captures[0].Value);
				}
				if (!sh.ContainsKey("type"))
					sh.Add("type","input");
				switch(sh["type"])
				{
					case "input":
					case "hidden":
						if (sh.ContainsKey("value"))
							ret.Add(sh["name"],sh["value"]);
						else	
							ret.Add(sh["name"],"");
						break;
					case "submit":
					case "checkbox": // none are currently checked, so ignore
						break;
					case "radio":
						if (sh.ContainsKey("checked"))
							ret.Add(sh["name"],sh["value"]);
						break;
					default:
						throw new Exception(sh["type"]);
				}
			}
			foreach (Match m2 in Regex.Matches(form.ToString(), "<select name=\"([^\"]*)\"(.*?)</select>", RegexOptions.Singleline))
			{
				Match option = Regex.Match(m2.Groups[2].Captures[0].Value, "<option value=\"([^\"]*)\" selected>",RegexOptions.Singleline);
				if (option.Success)
				{
					ret.Add(m2.Groups[1].Captures[0].Value, option.Groups[1].Captures[0].Value);
				}
			}
			return ret;
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

	public class Bugzilla
	{
		string cachepath = "cache";
		string root = "";

		CookieContainer cookies = null;
		
		public Bugzilla(string root)
		{
			this.root = root;
		}

		public bool login(string username, string password)
		{
			if (cookies == null)
			{
				FileInfo f=new FileInfo("cookies.dat");
				if (f.Exists)
				{
					Stream s=f.Open(FileMode.Open);
					BinaryFormatter b=new BinaryFormatter();
					cookies = (CookieContainer)b.Deserialize(s);
					s.Close();
				}
			}

			if (cookies==null || cookies.GetCookieHeader(new System.Uri(root)).IndexOf("Bugzilla_login")==-1)
			{
				string postData="Bugzilla_login="+username+"&Bugzilla_password="+password+"&Bugzilla_remember=on&Bugzilla_restrictlogin=on&GoAheadAndLogIn=1&GoAheadAndLogIn=Log+in";
				ASCIIEncoding encoding=new ASCIIEncoding();
				byte[]  data = encoding.GetBytes(postData);

				HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(root+"index.cgi");
				myRequest.Method = "POST";
				myRequest.ContentType="application/x-www-form-urlencoded";
				myRequest.ContentLength = data.Length;
				cookies = new CookieContainer();
				myRequest.CookieContainer = cookies;

				Stream newStream=myRequest.GetRequestStream();
				newStream.Write(data,0,data.Length);
				newStream.Close();

				HttpWebResponse wre = (HttpWebResponse) myRequest.GetResponse();
				StreamReader sr = new StreamReader(wre.GetResponseStream(), Encoding.ASCII);
				string ret = "";
				try
				{
					ret = sr.ReadToEnd();
				}
				catch
				{
					Console.WriteLine("Exception while reading bug");
				}
				sr.Close();
				if (ret.IndexOf("Logged In")==-1)
				{
					TextWriter outFile = new StreamWriter("login-test");
					outFile.Write(ret);
					outFile.Close();
					return false;
				}
				else
				{
					FileInfo f=new FileInfo("cookies.dat");
					Stream s=f.Open(FileMode.Create);
					BinaryFormatter b=new BinaryFormatter();
					b.Serialize(s,cookies);
					s.Close();
					return true;
				}
			}
			return true;
		}

		private string getData(string url, string cache)
		{
			return getData(url,cache,false);
		}

		private string getData(string url, string cache, bool ignorecache)
		{
			string path = Path.Combine(cachepath,cache);
			string ret = "";
			FileInfo fi = new FileInfo(path);
			if (ignorecache || !fi.Exists)
			{
				Console.WriteLine("grabbing {0}",url);
				HttpWebRequest wr = (HttpWebRequest) WebRequest.Create(url);
				if (cookies!=null)
					wr.CookieContainer = cookies;
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

		public string getBug(int id)
		{
			return getBug(id,false);
		}

		public string getBug(int id, bool ignorecache)
		{
			return getData(root+"show_bug.cgi?id="+id, String.Concat(id), ignorecache);
		}

		public string simpleDupe(int id)
		{
			return getData(root+"dupfinder/simple-dup-finder.cgi?id="+id, String.Concat(id)+"-dupe");
		}

		public string corebugs()
		{
			string corelist = getData(root+"reports/core-bugs-today.cgi","corebugs");
			Match m = Regex.Match(corelist,"("+root+"buglist.cgi\\?bug_id=[^\"]+)");
			return getData(m.ToString(),"corebugs-real");
		}

		public bool changeBug(Hashtable values)
		{
			StringBuilder query = new StringBuilder();
			foreach(string s in values.Keys)
			{
				Console.WriteLine("{0} = {1}",s,values[s]);
				if (query.Length!=0)
					query.Append("&");
				query.AppendFormat("{0}={1}",s,values[s]);
			}
			ASCIIEncoding encoding=new ASCIIEncoding();
			byte[]  data = encoding.GetBytes(query.ToString());

			HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(root+"process_bug.cgi");
			myRequest.Method = "POST";
			myRequest.ContentType="application/x-www-form-urlencoded";
			myRequest.ContentLength = data.Length;
			myRequest.CookieContainer = cookies;

			Stream newStream=myRequest.GetRequestStream();
			newStream.Write(data,0,data.Length);
			newStream.Close();

			HttpWebResponse wre = (HttpWebResponse) myRequest.GetResponse();
			StreamReader sr = new StreamReader(wre.GetResponseStream(), Encoding.ASCII);
			string ret = "";
			try
			{
				ret = sr.ReadToEnd();
			}
			catch
			{
				Console.WriteLine("Exception while reading bug");
			}
			sr.Close();
			TextWriter outFile = new StreamWriter("change-test");
			outFile.Write(ret);
			outFile.Close();
			return false;
		}
	}
}
