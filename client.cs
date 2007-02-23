using System;
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
		
		public void invoke(object r)
		{
			Response re = null;
			if (next!=null)
				re = (Response)next;
			//print();
			//Console.WriteLine("Invoking {0} of {1} ({2} {3})",call.Method, call.Target,input,re);
			call(r,input,re);
		}

		public static void invoke(Response r, object val)
		{
			if (r!=null)
				r.invoke(val);
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
				bugz.getBug(this.id,ignorecache,new Response(getRawResponse,r));
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
			string pattern = "<pre id=\"comment_text_(\\d+)\">(.*?)</pre>";
			ArrayList ret = new ArrayList();
			foreach (Match m in Regex.Matches(s, pattern, RegexOptions.Singleline))
			{
				//Console.WriteLine(m.Groups[0].Captures.Count);
				//Console.WriteLine(m.Groups[0].Captures[0].Value);
				ret.Add(m.Groups[0].Captures[0].Value);
			}
			//Console.WriteLine(ret.Count);
			this.comments = (string[])ret.ToArray(typeof(string));
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
			string check = (string)res;
			string pattern = @"<td>\s+<b>([^<:]+):</b>\s+</td>\s+<td>(.*?)</td>";
			this.values = new StringHash();
			foreach (Match m in Regex.Matches(check, pattern, RegexOptions.Singleline))
			{
				//Console.WriteLine(m.ToString());
				//Console.WriteLine(m.Groups[1].Captures[0].Value);
				string value = Bug.stripAll(m.Groups[2].Captures[0].Value);
				//Console.WriteLine(value);
				this.values.Add(m.Groups[1].Captures[0].Value.Trim(),value);
			}
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

		private void parseSearchResults(object curr, object input, Response chain)
		{
			StringHash[] core = Bug.tableParser((string)curr);
			//throw new Exception();
			ArrayList bugs = new ArrayList();
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
			chain.invoke(bugs.ToArray(typeof(Bug)));
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
							case "NEW":
								b.values[key] = h[key];
								break;
							case "CLOS":
								b.values[key] = "CLOSED";
								break;
							case "REOP":
								b.values[key] = "REOPENED";
								break;
							default:
								b.values[key] = h[key];
								break;
								//throw new Exception(h[key]);
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
							case "NOTA":
								b.values[key] = "NOTABUG";
								break;
							case "OBSO":
								b.values[key] = "OBSOLETE";
								break;
							case "INVA":
								b.values[key] = "INVALID";
								break;
							case "NOTX":
								b.values[key] = "NOTXIMIAN";
								break;
							default:
								b.values[key] = h[key];
								break;
								//throw new Exception(h[key]);
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
							case "maj":
								b.values["Severity"] = "major";
								break;
							case "nor":
								b.values["Severity"] = "normal";
								break;
							case "enh":
								b.values["Severity"] = "enhancement";
								break;
							case "tri":
								b.values["Severity"] = "trivial";
								break;
							case "blo":
								b.values["Severity"] = "blocker";
								break;
							default:
								b.values["Severity"] = h[key];
								break;
								//throw new Exception(h[key]);
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
							case "Nor":
								b.values["Priority"] = "Normal";
								break;
							case "Urg":
								b.values["Priority"] = "Urgent";
								break;
							default:
								b.values["Priority"] = h[key];
								break;
								//throw new Exception(h[key]);
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
				throw new Exception("table match failure");
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
						Console.WriteLine("row: {0} = {1}","ID",current["ID"]);
						break;
					}*/
					/*if (rows.Count>30)
						throw new Exception();*/
				}
			}
			return (StringHash[])rows.ToArray(typeof(StringHash));
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
			chain.invoke(ret);
		}
	}


	public class Stacktrace
	{
		const string pattern = "#(\\d+)\\s+(?:0x[\\da-f]+ in <span class=\"trace-function\">([^<]+)</span>\\s+\\([^\\)]*?\\)\\s+(?:at\\s+([^:]+:\\d+)|from\\s+([^ \n\r]+))?|<a name=\"stacktrace\"></a><span class=\"trace-handler\">&lt;(signal handler) called&gt;</span>)"; //(?:(?)|
		
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
					if ((last!=null && tostore[0]==last) || tostore[0] == "__kernel_vsyscall" || tostore[0] =="raise" || tostore[0] == "abort" || tostore[0] == "g_free" || tostore[0] == "memcpy" || tostore[0] == "NSGetModule")
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
		}

		private HttpWebRequest genRequest(string path)
		{
			HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(root+path);
			myRequest.UserAgent = "Pyro bug triager/0.1";
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

		private void getData(string url, string cache, Response r) {getData(url,cache,false,r);}
		private void getData(string url, string cache, bool ignorecache, Response chain)
		{
			string path = Path.Combine(cachepath,cache);
			FileInfo fi = new FileInfo(path);
			Console.WriteLine("grabbing {1}{0} ({2})",url,root,path);
			if (ignorecache || !fi.Exists)
			{
				getDataState state = new getDataState();
				Console.WriteLine("\nNEW!");
				state.req = genRequest(url);
				state.path = path;
				state.req.BeginGetResponse(new AsyncCallback(getDataCallback),new Response(getDataShim,chain,state));
			}
			else
			{
				TextReader inFile = new StreamReader(path);
				string ret = inFile.ReadToEnd();
				inFile.Close();
				chain.invoke(ret);
			}
		}

		private void getDataCallback(IAsyncResult ar)
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
			TextWriter outFile = new StreamWriter(st.path);
			outFile.Write(strip(ret));
			outFile.Close();
			Response.invoke(r,ret);
		}

		private void getDataShim(object res, object input, Response chain)
		{
			//throw new Exception();
			chain.invoke(res);
		}

		public string bugPath(int id)
		{
			return Path.GetFullPath(Path.Combine(cachepath,String.Concat(id)));
		}

		public void getBug(int id, Response r) { getBug(id,false, r);}
		public void getBug(int id, bool ignorecache, Response r)
		{
			getData("show_bug.cgi?id="+id, bugPath(id), ignorecache, r);
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
			getData(m.ToString(),"corebugs-real",true,r);
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
			getData("buglist.cgi?query="+System.Web.HttpUtility.UrlEncode(query.ToString()),name.ToString(),r);
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
					st.todo.Push(b);
				else if (b.stackhash == hash)
				{
					Response.invoke(r,b);
					return;
				}
			}
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
