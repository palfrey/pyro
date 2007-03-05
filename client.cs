using System;
using System.Xml;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Data;
using Mono.Data.SqliteClient;

namespace Pyro
{
	public class StringHash: Dictionary <string,string> /*Hashtable*/
	{
	}
		
	public delegate void GenericResponse(object resp, object input, Response chain);

	public class Response
	{
		public GenericResponse call;
		public object next;
		public object input;

		private Response(){}
		public Response(GenericResponse gr): this(gr,null,null) {}
		public Response(GenericResponse gr, object next): this(gr,next,null) {}
		public Response(GenericResponse gr, object next, object d)
		{
			call = gr;
			this.next = next;
			input = d;
			//Console.WriteLine("Creating {0} of {1}",call.Method, call.Target);
		}

		public void print()
		{
			int ind = 0;
			Response curr = this;
			Console.WriteLine("");
			while (curr!=null)
			{
				for(int i=0;i<ind;i++)
					Console.Write("\t");
				Console.WriteLine("{0}: {1} of {2}",ind, curr.call.Method, curr.call.Target);
				if (curr.next!=null)
				{
					ind++;
					curr = (Response)curr.next;
				}
				else
					break;
			}
			Console.WriteLine("");
		}
		
		public virtual void invoke(object r)
		{
			Response re = null;
			if (next!=null)
				re = (Response)next;
			//print();
			Console.WriteLine("Invoking {0} of {1} ({2} {3})",call.Method, call.Target,input,re);
			call(r,input,re);
		}

		public static void invoke(Response r, object val)
		{
			if (r!=null)
				r.invoke(val);
		}
	}

	public class VoidResponse: Response
	{	
		public VoidResponse(GenericResponse gr): base(gr,null,null) {}
		public VoidResponse(GenericResponse gr, object next): base(gr,next,null) {}
		public VoidResponse(GenericResponse gr, object next, object d):base(gr,next,d){}

		public override void invoke(object r)
		{
			base.invoke(null);
		}
	}

	public class Bug
	{
		StringHash values = null;
		string[] comments = null;
		int _id;
		public int dupid = -1;

		string _raw = null;
		public string stackhash = null;
		Bugzilla bugz = null;

		public int id
		{
			get {
				return this._id;
			}
		}

		public void clearRaw()
		{
			_raw = null;
		}

		public void setStackHash(Stacktrace st)
		{
			BugDB.DB.setStackHash(id,st);
			stackhash = st.getHash();
		}

		public void setStackHash(string st)
		{
			stackhash = st;
		}

		public static Bug getExisting(int id)
		{
			return BugDB.DB.getExisting(id);
		}

		public void setValue(string s, string val)
		{
			if (val == "")
				return;
			if (values == null)
				values = new StringHash();
			values[s] = val;	
		}
		
		public void getRaw(Response r) { getRaw(false,r);}
		public void getRaw(bool ignorecache, Response r)
		{
			if (this._raw == null || ignorecache)
				bugz.getBug(new int [] {this.id},ignorecache,new Response(getRawResponse,r));
			else
				Response.invoke(r,this._raw);
		}

		public void getRawResponse(object data, object input, Response chain)
		{
			this._raw = (string)data;
			this.values = null;
			BugDB.DB.setExisting(this.id);
			BugDB.DB.setValues(this.id,null);
			chain.invoke(data);
		}

		public void refresh(Response r)
		{
			getRaw(true, r);
		}

		public string localpath()
		{
			return bugz.bugPath(this.id);
		}
		
		public Bug(int id, Bugzilla bugz)
		{
			this._id = id;
			this.bugz = bugz;
		}

		void getComments(Response r)
		{
			if (this.comments == null)
				getRaw(new Response(getCommentsCallback,r));
			else
				Response.invoke(r,this.comments);
		}

		private void getCommentsCallback(object o, object input, Response r)
		{
			string s = (string)o;
			string pattern = "<thetext>(.*?)</thetext>";
			List<string> ret = new List<string>();
			//Console.WriteLine(s);
			foreach (Match m in Regex.Matches(s, pattern, RegexOptions.Singleline))
			{
				//Console.WriteLine(m.Groups[0].Captures.Count);
				//Console.WriteLine(m.Groups[0].Captures[0].Value);
				ret.Add(m.Groups[0].Captures[0].Value);
			}
			if (ret.Count==0)
				throw new Exception();
			Console.WriteLine("Comments: {0}",ret.Count);
			this.comments = ret.ToArray();
			Response.invoke(r,this.comments);
		}
	
		public void getValues(string idx, Response r)
		{
			if (this.values==null || (idx!=null && this._raw == null && !this.values.ContainsKey(idx)))
			{
				getRaw(new Response(getValuesResponse,r));
			}
			else
				Response.invoke(r,values);
		}

		public void getValuesResponse(object res, object input,Response r)
		{
			/*string check = (string)res;
			string pattern = @"<td>\s+<b>([^<:]+):</b>\s+</td>\s+<td>(.*?)</td>";*/
			StringHash mappings = new StringHash();
			mappings.Add("long_desc",null); /* ignore comments */
			mappings.Add("attachment",null); /* ignore attachments */
			mappings.Add("bug_status","Status");
			mappings.Add("priority","Priority");
			mappings.Add("bug_severity","Severity");
			this.values = xmlParser((string)res,"bug",mappings)[0];
			/*foreach (Match m in Regex.Matches(check, pattern, RegexOptions.Singleline))
			{
				//Console.WriteLine(m.ToString());
				//Console.WriteLine(m.Groups[1].Captures[0].Value);
				string value = Bug.stripAll(m.Groups[2].Captures[0].Value);
				//Console.WriteLine(value);
				this.values.Add(m.Groups[1].Captures[0].Value.Trim(),value);
			}*/
			BugDB.DB.setValues(id,values);
			Response.invoke(r,values);
		}

		public void describe()
		{
			Console.WriteLine("id = {0}",id);
			if (values == null)
				return;
			foreach(string s in values.Keys)
			{
				Console.WriteLine("{0} = {1}",s,values[s]);
			}
		}
		
		static string stripAll(string inVal)
		{
			string pattern = "<[^>]*>";
			//string pattern = @"</?(?i:a|script|embed|object|frameset|frame|iframe|meta|link|style|span)(.|\n)*?>";
			return Regex.Replace(inVal, pattern, "").Trim();
		}

		public void triageable(Response r)
		{
			if (BugDB.DB.done(id))
				Response.invoke(r,false);
			else	
				getValues("Status",new Response(triageableResponse,r));
		}

		private void triageableResponse(object res, object input, Response r)
		{
			StringHash values = (StringHash)res;
			if ((values["Status"]!="UNCONFIRMED" && values["Status"]!="NEEDINFO") || values["Severity"]!="critical" || values["Priority"]!="High")
			{
				Response.invoke(r,false);
				return;
			}
			if (_raw!=null)
			{
				if (_raw.IndexOf("Traceback (most recent call last):")!=-1) // python, can't triage
				{
					Response.invoke(r,false);
					return;
				}
				else if (_raw.IndexOf("Thanks for taking the time to report this bug")!=-1) // triaged!
				{
					Response.invoke(r,false);
					return;
				}
			}
			Response.invoke(r,true);
		}
		
		public void getStacktrace(Response sr)
		{
			if (this.id == 0)
				throw new Exception();
			getComments(new Response(getStacktraceResponse,sr));
		}

		public void getStacktraceResponse(object curr, object input, Response chain)
		{
			string[] comm = (string[]) curr;
			Stacktrace st = new Stacktrace(this.id,comm[0]);
			this.setStackHash(st);
			chain.invoke(st);
		}

		public void similar(Response r)
		{
			getStacktrace(new Response(similarResponse,r));
		}

		private void similarResponse(object curr, object input, Response chain)
		{
			bugz.stackDupe((Stacktrace)curr, new Response(parseSearchResults,chain));
		}

		StringHash mappings = null;

		private void parseSearchResults(object curr, object input, Response chain)
		{
			if (mappings == null)
			{
				mappings = new StringHash();
				mappings.Add("bz:id","ID");
			}
			
			StringHash[] core = Bug.xmlParser((string)curr,"bz:bug",mappings);
			//throw new Exception();
			List<Bug> bugs = new List<Bug>();
			bool dolimit = true;
			int limit = 15;
			if (input!=null)
				dolimit = (bool)input;
			foreach(StringHash h in core)
			{
				int id = System.Convert.ToInt32(h["ID"],10);
				Bug b = Bug.getExisting(id);
				if (b == null)
				{
					b = new Bug(id,this.bugz);
					b.values = new StringHash();
					assignKeys(b,h);
				}
				bugs.Add(b);
				Console.WriteLine("new bug {0}",b.id);
				if (dolimit)
				{
					limit--;
					if (limit == 0)
						break;
				}
			}
			//throw new Exception();
			chain.invoke(bugs.ToArray());
		}

		public void corebugs(Response r)
		{
			bugz.corebugs(new Response(parseSearchResults, r, false));
		}

		public void product(string name, Response r)
		{
			bugz.product(name, new Response(parseSearchResults, r, false));
		}

		public void numbered(int id, int id2, Response r)
		{
			bugz.numbered(id,id2, new Response(parseSearchResults, r, false));
		}

		Dictionary<string,Dictionary<string,string>> keys = null;

		void assignKeys(Bug b,StringHash h)
		{
			if (keys == null)
			{
				keys = new Dictionary<string,Dictionary<string,string>>();
				
				Dictionary<string,string> Status = new Dictionary<string,string>();
				Status.Add("RESO","RESOLVED");
				Status.Add("NEED","NEEDINFO");
				Status.Add("UNCO","UNCONFIRMED");
				Status.Add("CLOS","CLOSED");
				Status.Add("REOP","REOPENED");
				keys.Add("Status",Status);

				Dictionary <string,string> Resolution = new Dictionary<string,string>();
				Resolution.Add("FIXE","FIXED");
				Resolution.Add("INCO","INCOMPLETE");
				Resolution.Add("NOTG","NOTGNOME");
				Resolution.Add("NOTA","NOTABUG");
				Resolution.Add("OBSO","OBSOLETE");
				Resolution.Add("INVA","INVALID");
				Resolution.Add("NOTX","NOTXIMIAN");
				keys.Add("Resolution",Resolution);

				Dictionary <string,string> Severity = new Dictionary<string,string>();
				Severity.Add("cri","critical");
				Severity.Add("maj","major");
				Severity.Add("nor","normal");
				Severity.Add("enh","enhancement");
				Severity.Add("tri","trivial");
				Severity.Add("blo","blocker");
				keys.Add("Sev",Severity);

				Dictionary <string,string> Priority = new Dictionary<string,string>();
				Priority.Add("Hig","High");
				Priority.Add("Nor","Normal");
				Priority.Add("Urg","Urgent");
				keys.Add("Pri",Priority);
			}
			foreach(string key in h.Keys)
			{
				switch(key)
				{
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
						goto default;
					}
					default:
						if (keys.ContainsKey(key))
						{
							if (keys[key].ContainsKey(h[key]))
							{
								b.values[key] = keys[key][h[key]];
								break;
							}
						}
						b.values[key] = h[key];
						break;
				}
			}
								
		}

		static StringHash[] xmlParser(string input, string separator)
		{
			return xmlParser(input,separator,new StringHash());
		}

		static StringHash[] xmlParser(string input, string separator, StringHash mappings)
		{
			List<StringHash> rows = new List<StringHash>();
			input = input.Replace("<�,","");
			//Console.WriteLine(input);
			XmlTextReader reader = new XmlTextReader(new StringReader(input));
			string top = reader.NameTable.Add(separator);
			while (reader.Read()) 
			{
				if (reader.NodeType == XmlNodeType.Element) 
				{
					if (reader.Name.Equals(top)) 
					{
						StringHash ret = new StringHash();
						reader.Read();
						string element = null;
						while (!reader.Name.Equals(top))
						{
							switch(reader.NodeType)
							{
								case XmlNodeType.Element:
									element = reader.Name;
									if (mappings.ContainsKey(element))
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
							reader.Read();
						}
						rows.Add(ret);
					}      
				}
			}
			return rows.ToArray();
		}


		static StringHash[] tableParser(string input)
		{
			List<StringHash> rows = new List<StringHash>();
			Match tab = Regex.Match(input, "<table(.*?)</table>",RegexOptions.Singleline);
			if (!tab.Success)
				throw new Exception("table match failure");
			string table = tab.ToString();
			string [] headers = null;
			List<string> hdrs = new List<string>();
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
					headers = hdrs.ToArray();
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
						Console.WriteLine("row: {0} = {1}","ID",current["ID"]);
						break;
					}*/
					/*if (rows.Count>30)
						throw new Exception();*/
				}
			}
			return rows.ToArray();
		}

		public void setBadStacktrace(Response r)
		{
			refresh(new Response(setBadRefresh,r));
		}

		private void setBadRefresh(object o, object input, Response r)
		{
			getValues("Status",new Response(setBadGetVal,r));
		}

		private void setBadGetVal(object o, object input, Response r)
		{
			if (((StringHash)o)["Status"]!="NEEDINFO")
				parseInput(new Response(setBadStacktraceResponse,r));
			else
			{
				Console.WriteLine("already marked as needinfo");
				Response.invoke(r,null);
			}
		}

		private void setBadStacktraceResponse(object curr, object input, Response r)
		{
			StringHash orig = (StringHash)curr;
			orig["comment"] = @"Thanks for taking the time to report this bug.
Unfortunately, that stack trace is missing some elements that will help a lot
to solve the problem, so it will be hard for the developers to fix that crash.
Can you get us a stack trace with debugging symbols? Please see
http://live.gnome.org/GettingTraces for more information on how to do so.
Thanks in advance!";
			orig["knob"] = "needinfo";
			bugz.changeBug(orig);
			refresh(r);
		}
		
		public void setDupe(Response r, Bug dupe)
		{
			refresh(new Response(setDupeRefresh,r,dupe));
		}

		private void setDupeRefresh(object o, object input, Response r)
		{
			getValues("Status",new Response(setDupeGetVal,r,input));
		}

		private void setDupeGetVal(object o, object input, Response r)
		{
			if (((StringHash)o)["Status"]!="RESOLVED")
				parseInput(new Response(setDupeResponse,r,input));
			else
			{
				Console.WriteLine("already resolved");
				Response.invoke(r,null);
			}
		}

		private void setDupeResponse(object curr, object input, Response r)
		{
			StringHash orig = (StringHash)curr;
			if (values["Status"] == "NEEDINFO")
				orig["comment"] = "Thanks for taking the time to report this bug.\nThis particular bug has already been reported into our bug tracking system, but the maintainers need more information to fix the bug. Could you please answer the questions in the other report in order to help the developers?";
			else
    			orig["comment"] = "Thanks for the bug report. This particular bug has already been reported into our bug tracking system, but please feel free to report any further bugs you find";
			orig["knob"] = "duplicate";
			orig["dup_id"] = String.Concat(((Bug)input).id);
			foreach(string s in orig.Keys)
			{
				Console.WriteLine("{0} = {1}",s,orig[s]);
			}
			bugz.changeBug(orig);
			refresh(r);
		}
		
		private void parseInput(Response r)
		{
			getRaw(new Response(parseInputResponse,r));
		}

		private void parseInputResponse(object curr, object input, Response chain)
		{
			Match form = Regex.Match((string)curr, "<form name=\"changeform\" method=\"post\" action=\"process_bug.cgi\">(.*?)</form>",RegexOptions.Singleline);
			StringHash ret = new StringHash();
			if (!form.Success)
				throw new Exception("form match failure");
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
			chain.invoke(ret);
		}
	}


	public class Stacktrace
	{
		//const string pattern = "#(\\d+)\\s+(?:0x[\\da-f]+ in <span class=\"trace-function\">([^<]+)</span>\\s+\\([^\\)]*?\\)\\s+(?:at\\s+([^:]+:\\d+)|from\\s+([^ \n\r]+))?|<a name=\"stacktrace\"></a><span class=\"trace-handler\">&lt;(signal handler) called&gt;</span>)"; //(?:(?)|
		const string pattern = "#(\\d+)\\s+(?:0x[\\da-f]+ in ([^\\s]+)\\s+\\([^\\)]*?\\)\\s+(?:at\\s+([^:]+:\\d+)|from\\s+([^ \n\r]+))?|&lt;(signal handler) called&gt;)"; //(?:(?)|
		
		string raw = "";
		public List<string[]> content = null;
		public int id;
		
		public Stacktrace(int id, string data)
		{
			this.raw = data;
			this.id = id;
			int limit = 0;
			this.content = new List<string[]>();
			string last = null;
			bool seen_signal = false;
			int idx = -1;
			foreach (Match m in Regex.Matches(this.raw, Stacktrace.pattern, RegexOptions.Singleline))
			{
				int new_idx = System.Convert.ToInt32(m.Groups[1].Captures[0].Value, 10);
				if (idx!=-1 && new_idx<idx)
				{
					if(seen_signal)
						break;
					this.content = new List<string[]>();
					limit = 0;
				}
				idx = new_idx;	
				if (m.Groups[2].Captures.Count!=0)
				{
					string[] tostore = new string[2];
					tostore[0] = m.Groups[2].Captures[0].Value;
					if ((last!=null && tostore[0]==last) || tostore[0] == "__kernel_vsyscall" || tostore[0] =="raise" || tostore[0] == "abort" || tostore[0] == "g_free" || tostore[0] == "memcpy" || tostore[0] == "NSGetModule" || tostore[0] == "??")
						continue;

					if (m.Groups[3].Captures.Count!=0)
						tostore[1] = m.Groups[3].Captures[0].Value;
					else if (m.Groups[4].Captures.Count!=0)
						tostore[1] = m.Groups[4].Captures[0].Value;
					else
						tostore[1] = "";
					if (seen_signal)
					{
						this.content.Add(tostore);
						last = tostore[0];
						limit++;
						if (limit==6)
							break;
					}
				}
				else if (m.Groups[5].Captures.Count!=0)
				{
					Console.WriteLine(" signal handler!");
					Console.WriteLine(m.Groups[5].Captures[0].Value);
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
				string [] one = this.content[idx];
				string [] two = right.content[idx];
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

		public string getHash()
		{
			StringBuilder ret = new StringBuilder();
			foreach (string[] s in this.content)
			{
				ret.Append(String.Format("{0}:{1}:",s[0],s[1]));
			}
			return ret.ToString();
		}

		public bool usable()
		{
			if (this.content.Count == 0)
				return false;
			if (this.content[0][0] == "poll" && this.content[1][0] == "g_main_context_check") // evolution bugs that we're hacking around
				return false;
			return true;
		}

		public void print()
		{
			Console.WriteLine("Stacktrace for {0}",this.id);
			foreach(string[] s in this.content)
			{
				Console.WriteLine("{0} {1}",s[0],s[1]);
			}
		}
	}

	public class Bugzilla
	{
		string cachepath = "cache";
		string root = "";
		
		WebProxy wp;

		private bool _loggedIn = false;
		public bool loggedIn
		{
			get
			{
				return _loggedIn;
			}
		}

		CookieContainer cookies = null;
		
		public Bugzilla(string root)
		{
			this.root = root;
			wp = null; //new WebProxy("taz",8118);
			FileInfo f=new FileInfo("cookies.dat");
			if (f.Exists)
				_loggedIn = true;
		}

		private HttpWebRequest genRequest(string path)
		{
			HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(root+path);
			myRequest.UserAgent = "Pyro bug triager/0.2";
			if (wp!=null)
				myRequest.Proxy = wp;
			myRequest.CookieContainer = cookies;
			return myRequest;
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

				cookies = new CookieContainer();
				HttpWebRequest myRequest = genRequest("index.cgi");
				myRequest.Method = "POST";
				myRequest.ContentType="application/x-www-form-urlencoded";
				myRequest.ContentLength = data.Length;

				Stream newStream=myRequest.GetRequestStream();
				newStream.Write(data,0,data.Length);
				newStream.Close();

				Console.WriteLine(myRequest.Headers.ToString());
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
					_loggedIn = true;
					return true;
				}
			}
			return true;
		}


		private struct getDataState
		{
			public HttpWebRequest req;
			public string path;
		}
		
		public static string strip(string inVal)
		{
			string val = Regex.Replace(inVal,"<script type=\"text/javascript\">.*?</script>","",RegexOptions.Singleline);
			val = Regex.Replace(val,"<link href=\"skins/standard/global.css\" rel=\"stylesheet\" type=\"text/css\">","<style type=\"text/css\">\n.trace-function { color: #cc0000; }\n.trace-handler  { color: #4e9a06; }\n</style>");


			string pattern = @"</?(?i:img|base|script|embed|object|frameset|frame|iframe|meta|link)(.|\n)*?>";
			return Regex.Replace(val, pattern, "").Trim();
		}

		private string path(string cache)
		{
			return Path.GetFullPath(Path.Combine(cachepath,cache));
		}

		private bool hasData(string cache)
		{
			return new FileInfo(path(cache)).Exists;
		}

		private string readData(string cache)
		{
			TextReader inFile = new StreamReader(path(cache));
			string ret = inFile.ReadToEnd();
			inFile.Close();
			return ret;
		}

		private void getData(string url, string cache, Response r) {getData(url,cache,false,r);}
		private void getData(string url, string cache, bool ignorecache, Response chain)
		{
			Console.WriteLine("grabbing {1}{0}",url,root);
			if (ignorecache || !hasData(cache))
			{
				getDataState state = new getDataState();
				Console.WriteLine("\nNEW!");
				state.req = genRequest(url);
				state.path = path(cache);
				//writePath(state.path,""); /* test! */
				state.req.BeginGetResponse(new AsyncCallback(getDataCallback),new Response(getDataShim,chain,state));
			}
			else
				chain.invoke(readData(cache));
		}

		private void writePath(string path,string data)
		{
			TextWriter outFile = new StreamWriter(path);
			Console.WriteLine("Writing {0}",path);
			outFile.Write(data);
			outFile.Close();
		}

		private void getDataCallback(IAsyncResult ar)
		{
			try
			{
				string ret = "";
				Response r = (Response)ar.AsyncState;
				getDataState st = (getDataState)r.input;
				HttpWebResponse wre = (HttpWebResponse) st.req.EndGetResponse(ar);
				StreamReader sr = new StreamReader(wre.GetResponseStream(), Encoding.ASCII);
				Console.WriteLine("\nResponse for {0}\n",st.path);
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
				Console.WriteLine("Got {0}",st.path);
				writePath(st.path,strip(ret));
				Response.invoke(r,ret);
			}
			catch (Exception e)
			{
				/* necessary because of http://bugzilla.gnome.org/show_bug.cgi?id=395709 */
				Console.WriteLine("async exception");
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
				Environment.Exit(1);
				throw e;
			}
		}

		private void getDataShim(object res, object input, Response chain)
		{
			chain.invoke(res);
		}

		public string bugPath(int id)
		{
			return Path.GetFullPath(Path.Combine(cachepath,String.Concat(id)));
		}

		private struct BugList
		{
			public Stack<int> todo;
			public int[] complete;
			public bool ignorecache;
		}
		
		public void getBug(int[] id, Response r) { getBug(id,false, r);}

		public void getBug(int[] id, bool ignorecache, Response r)
		{
			BugList bl = new BugList();
			bl.complete = id;
			bl.todo = new Stack<int>(id);
			bl.ignorecache = ignorecache;
			getBug(bl,r);
		}

		private void getBug(BugList bl, Response r)
		{
			StringBuilder grab = new StringBuilder("show_bug.cgi?ctype=xml");
			StringBuilder path = new StringBuilder(Path.GetFullPath(Path.Combine(cachepath,"bugs")));
			bool find = false;
			while(bl.todo.Count>0)
			{
				int x = bl.todo.Pop();
				if(!hasData(bugPath(x)))
				{
					grab.AppendFormat("&id={0}",x);
					path.AppendFormat("-{0}",x);
					find = true;
					if (path.Length>150) /* sensible limit given varying path limits */
						break;
				}
			}
			if (find)
				getData(grab.ToString(), path.ToString(), bl.ignorecache, new Response(splitBugs,r,bl));
			else
				splitBugs(null,bl,r);
		}

		private void splitBugs(object res, object input, Response r)
		{
			BugList bl = (BugList)input;
			if (res!=null)
			{
				bool found = false;
				XmlTextReader reader = new XmlTextReader(new StringReader((string)res));
				string top = reader.NameTable.Add("bug");
				while (reader.Read()) 
				{
					if (reader.NodeType == XmlNodeType.Element) 
					{
						if (reader.Name.Equals(top)) 
						{
							string data = reader.ReadOuterXml();
							Match tab = Regex.Match(data, "<bug_id>(\\d+)</bug_id>");
							if (!tab.Success)
								throw new Exception("bug_id match failure");
							int id = Int32.Parse(tab.Groups[1].Captures[0].Value);
							Console.WriteLine("found {0}",id);
							writePath(bugPath(id),data);
							found = true;
						}
					}
				}
				if (!found)
					throw new Exception();
			}
			if (bl.todo.Count>0)
				getBug(bl,r);
			else
			{
				StringBuilder ret = new StringBuilder();
				foreach (int x in bl.complete)
					ret.Append(readData(bugPath(x)));
				Response.invoke(r,ret.ToString());	
			}
		}

		public void simpleDupe(int id, Response r)
		{
			getData("dupfinder/simple-dup-finder.cgi?id="+id, String.Concat(id)+"-dupe", r);
		}

		public void product(string name, Response r)
		{
			getData("buglist.cgi?query=product%3A"+name.Replace("+","%2B")+"+comment-count%3A0+status%3Aunconfirmed+severity%3Acritical+priority%3Ahigh",name,true,r);
		}

		public void numbered(int id, int id2, Response r)
		{
			getData(String.Format("buglist.cgi?query=comment-count%3A0+severity%3Acritical+priority%3Ahigh+bug-number%3E%3D{0}+bug-number%3C%3D{1}+status%3Aunconfirmed",id,id2),"numbered",true,r);
		}

		public void corebugs(Response r)
		{
			getData("reports/core-bugs-today.cgi","corebugs",true,new Response(corebugsMatcher,r));
		}

		private void corebugsMatcher(object res, object input, Response r)
		{
			string corelist = (string)res;
			Match m = Regex.Match(corelist,"(buglist.cgi\\?bug_id=[^\"]+)");
			Console.WriteLine(r);
			getData(m.ToString()+"&ctype=rdf","corebugs-real",false,r);
		}

		public void stackDupe(Stacktrace st, Response r)
		{
			StringBuilder query = new StringBuilder("meta-status:all");
			StringBuilder name = new StringBuilder("stackdupe");
			foreach(string[] s in st.content)
			{
				query.Append(" \""+s[0]+"\"");
				name.Append("-"+s[0].Replace("(","_").Replace(")","_"));
			}
			getData("buglist.cgi?ctype=rdf&query="+System.Web.HttpUtility.UrlEncode(query.ToString()),name.ToString(),r);
		}

		public bool changeBug(StringHash values)
		{
			StringBuilder query = new StringBuilder();
			foreach(string s in values.Keys)
			{
				Console.WriteLine("{0} = {1}",s,values[s].Replace("&#64;","%40"));
				if (query.Length!=0)
					query.Append("&");
				query.AppendFormat("{0}={1}",s,values[s].Replace("&#64;","%40"));
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
			Console.WriteLine("Doing bug update");

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
			outFile.Write(query.ToString());
			outFile.Write("\n\n");
			outFile.Write(ret);
			outFile.Close();
			Console.WriteLine("Bug update complete");
			return false;
		}
	}

	public class BugDB
	{
		static readonly BugDB instance = new BugDB();
		SqliteConnection dbcon = null;
		public static Bugzilla bugz =null;
		Dictionary<int,Bug> bugs=null;

		static BugDB()
		{
		}

		BugDB()
		{
			dbcon = new SqliteConnection("URI=file:bugs.db,version=3");
			dbcon.Open();
			IDbCommand dbcmd = new SqliteCommand("select name from sqlite_master where type='table' and name='bugs'",dbcon);
			IDataReader reader = dbcmd.ExecuteReader();
			if (!reader.Read())
			{
				dbcmd = new SqliteCommand("create table bugs(id integer primary key, done boolean, Status text, Priority text, Severity Text, stackhash text)",dbcon);
				dbcmd.ExecuteReader();
			}
			bugs = new Dictionary<int,Bug>();
		}

		public static BugDB DB
		{
			get
			{
				return instance;
			}
		}

		private struct SimilarTodo
		{
			public Bug old;
			public Stacktrace oldst;
			public Stack<Bug> todo;
		}
		
		public void similar(int id, Response r)
		{
			SimilarTodo st = new SimilarTodo();
			st.old = getExisting(id);
			st.old.getStacktrace(new Response(similarOldSt,r,st));
		}

		private void similarOldSt(object res, object input, Response r)
		{
			SimilarTodo st = (SimilarTodo) input;
			List<int> ids = new List<int>();
			st.oldst = (Stacktrace)res;
			st.todo = new Stack<Bug>();
			string hash = st.oldst.getHash();
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "select id from bugs where id<"+String.Concat(st.old.id);
			IDataReader reader = dbcmd.ExecuteReader();
			while (reader.Read())
			{
				Bug b = getExisting(reader.GetInt32(0));
				if (b.id == 0)
					continue;
				if (b.stackhash == null)
				{
					st.todo.Push(b);
					ids.Add(b.id);
					if (st.todo.Count>=12)
						break;
				}
				else if (b.stackhash == hash)
				{
					Response.invoke(r,b);
					return;
				}
			}
			if (st.todo.Count>0)
			{
				Console.WriteLine(st.todo.Count);
				bugz.getBug(ids.ToArray(),new VoidResponse(nextSimilar,r,st));
			}
			else
				nextSimilar(null,st,r);	
		}

		public bool done(int id)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "select done from bugs where id="+String.Concat(id);
			IDataReader reader = dbcmd.ExecuteReader();
			if (reader.Read())
				return reader.GetInt32(0)!=0;
			else
				return false;
		}

		public void setValues(int id, StringHash values)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			if (values == null)
				dbcmd.CommandText = "update bugs set Status=\"\",Priority=\"\",Severity=\"\"";
			else
			{
				dbcmd.CommandText = "update bugs set Status=\""+values["Status"]+"\"";
				if (values.ContainsKey("Priority"))
					dbcmd.CommandText += ",Priority=\""+values["Priority"]+"\"";
				else
					dbcmd.CommandText += ",Priority=\"\"";
				if (values.ContainsKey("Severity"))
					dbcmd.CommandText += ",Severity=\""+values["Severity"]+"\"";
				else	
					dbcmd.CommandText += ",Severity=\"\"";
			}
			dbcmd.CommandText += " where id="+String.Concat(id);
			dbcmd.ExecuteNonQuery();
		}
		
		public void setStackHash(int id, Stacktrace st)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "update bugs set stackhash=@hash where id="+String.Concat(id);
			dbcmd.Parameters.Add(new SqliteParameter("@hash",st.getHash()));	
			dbcmd.ExecuteNonQuery();
		}

		public void setDone(int id)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "update bugs set done=1 where id="+String.Concat(id);
			dbcmd.ExecuteNonQuery();
		}

		private void nextSimilar(object res, object input, Response r)
		{
			SimilarTodo st = (SimilarTodo) input;
			if (res!=null)
			{
				Stacktrace st2 = (Stacktrace)res;
				getExisting(st2.id).clearRaw();
				if (st2 == st.oldst)
				{
					Response.invoke(r,getExisting(st2.id));
					return;
				}
			}
			while (true)
			{
				if (st.todo.Count == 0)
				{
					Response.invoke(r,null);
					return;
				}
				Bug app = st.todo.Pop();
				if (app == null)
					throw new Exception(String.Concat(app.id));
				app.getStacktrace(new Response(nextSimilar,r,st));
				break;
			}
		}

		public Bug getExisting(int id)
		{
			if (bugs.ContainsKey(id))
				return bugs[id];
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "select Severity,Priority,Status,stackhash from bugs where id="+String.Concat(id);
			IDataReader reader = dbcmd.ExecuteReader();
			if (reader.Read())
			{
				Bug ret = new Bug(id,bugz);
				if (!reader.IsDBNull(0))
					ret.setValue("Severity",reader.GetString(0));
				if (!reader.IsDBNull(1))
					ret.setValue("Priority",reader.GetString(1));
				if (!reader.IsDBNull(2))
					ret.setValue("Status",reader.GetString(2));
				if (!reader.IsDBNull(3))
					ret.setStackHash(reader.GetString(3));
				bugs[id] = ret;
				return ret;
			}
			else
				return null;
		}

		public void setExisting(int id)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "select done from bugs where id="+String.Concat(id);
			IDataReader reader = dbcmd.ExecuteReader();

			if (!reader.Read())
			{
				IDbCommand dbcmd2 = dbcon.CreateCommand();
				dbcmd2.CommandText = "insert into bugs (id,done) values(@id,0)";
				dbcmd2.Parameters.Add(new SqliteParameter("@id",id));	
				dbcmd2.ExecuteNonQuery();
			}
		}
	}
}
