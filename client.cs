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
using NonValidating;

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
		public int depth = 1;

		private Response(){}
		public Response(GenericResponse gr): this(gr,null,null) {}
		public Response(GenericResponse gr, object next): this(gr,next,null) {}
		public Response(GenericResponse gr, object next, object d)
		{
			call = gr;
			this.next = next;
			input = d;
			if (next!=null)
				depth = ((Response)next).depth+1;
			//Console.WriteLine("Creating {0} of {1} ({2})",call.Method, call.Target, depth);
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
		
		public virtual void invoke(object curr)
		{
			Response re = null;
			if (next!=null)
				re = (Response)next;
			//print();
			Console.WriteLine("Invoking {0} of {1} (depth={2})",call.Method, call.Target,depth);
			/*if (depth == 4)
				throw new Exception();*/
			call(curr,input,re);
		}

		public static void invoke(Response r, object val)
		{
			if (r!=null)
				r.invoke(val);
			else
				Console.WriteLine("hit a null invoke");
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
		public const string sd = "-simpledupe";
		public StringHash values = null;
		string comments = null;
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

		public void setValues()
		{
			BugDB.DB.setValues(this);
		}

		public void setStackHash(Stacktrace st)
		{
			setStackHash(st.getHash());
		}

		public void setStackHash(string st)
		{
			stackhash = st;
			BugDB.DB.setStackHash(id,st);
			setValues();
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
		
		public void getRaw(Response r) { getRaw(-1,r);}
		public void getRaw(double max_age, Response r)
		{
			if (this._raw == null || bugz.isTooOld(localpath(),max_age))
				bugz.getBug(new int [] {this.id},max_age,new Response(getRawResponse,r));
			else
				Response.invoke(r,this._raw);
		}

		public void getRawResponse(object data, object input, Response chain)
		{
			this._raw = (string)data;
			this.values = null;
			//BugDB.DB.setValues(this.id,null);
			Response.invoke(chain,data);
		}

		public void refresh(Response r)
		{
			//getRaw(true, r);
			//getRaw(false, r);
			Response.invoke(r,null);
		}

		public void remove(object curr, object input, Response r)
		{
			//File.Delete(localpath());
			//throw new Exception();
			//BugDB.DB.remove(this.id);
			Response.invoke(r,input);
		}

		public string localpath()
		{
			return bugz.bugPath(this.id);
		}
		
		static StringHash mappings = null;

		public Bug(int id, Bugzilla bugz)
		{
			if (Bug.mappings == null)
			{
				Bug.mappings = new StringHash();
				mappings.Add("bz:id","ID");
				mappings.Add("attachment",null); /* ignore attachments */
				mappings.Add("bug_status","Status");
				mappings.Add("bug_id","id");
				mappings.Add("classification_id",null);
				mappings.Add("classification",null);
				mappings.Add("reporter_accessible",null);
				mappings.Add("initialowner_id",null);
				mappings.Add("creation_ts",null);
				mappings.Add("cclist_accessible",null);
				mappings.Add("reporter",null);
			}
			this._id = id;
			this.bugz = bugz;
		}

		public void buildBug(Response r) {buildBug(this,r);}
		void buildBug(object input,Response r)
		{
			if (values==null)
				getValues(null,new Response(doStack,r,input),input);
			else
				doStack(null,input,r);
		}

		private void doStack(object res, object input, Response r)
		{
			Bug bug = (Bug)input;
			bug.getStacktrace(new Response(checkDupe,r));
		}

		private void checkDupe(object curr, object input, Response r)
		{
			if (dupid!=-1 || values["Status"] != "RESOLVED" || (values.ContainsKey("resolution") && values["resolution"] != "DUPLICATE"))
				Response.invoke(r,this);
			else
				getDupid(new Response(gotDupid,r));
		}

		private void gotDupid(object curr, object input, Response r)
		{
			Response.invoke(r,this);
		}

		void getComments(Response r)
		{
			if (this.comments == null)
				parseInput(new Response(getCommentsCallback,r));
			else
				Response.invoke(r,this.comments);
		}

		private void getCommentsCallback(object o, object input, Response r)
		{
			StringHash s = (StringHash)o;
			this.comments = s["thetext"];
			Response.invoke(r,this.comments);
		}
	
		public void getValues(string idx, Response r) {getValues(idx,r,null);}
		public void getValues(string idx, Response r, object input)
		{
			if (this.values==null || (idx!=null && this._raw == null && !this.values.ContainsKey(idx)))
				parseInput(r, input);
			else
				Response.invoke(r,values);
		}

		public void describe()
		{
			Console.WriteLine("id = {0}",id);
			if (values == null)
				return;
			foreach(string s in values.Keys)
			{
				if (s!="thetext")
					Console.WriteLine("{0} = {1}",s,values[s]);
			}
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
			if ((values["Status"]!="UNCONFIRMED" && values["Status"]!="NEEDINFO") || values["bug_severity"]!="critical")
			{
				Response.invoke(r,false);
				return;
			}
			if (_raw!=null)
			{
				if (_raw.IndexOf("Thanks for taking the time to report this bug")!=-1) // triaged!
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
			string comm = (string) curr;
			Stacktrace st = new Stacktrace(this.id,comm);
			this.setStackHash(st);
			chain.invoke(st);
		}

		public void similar(Response r)
		{
			getStacktrace(new Response(similarResponse,r));
		}

		private void similarResponse(object curr, object input, Response chain)
		{
			bugz.stackDupe((Stacktrace)curr, new Response(parseSearchResults,chain,true));
		}

		private void parseSearchResults(object curr, object input, Response chain)
		{
			bool dolimit = false;
			if (input!=null)
				dolimit = (bool)input;
			int limit;
			if (dolimit)
				limit = 15;
			else
				limit = -1;

			List<Bug> bugs = new List<Bug>();
			if (curr != null)
			{
				if (((string)curr).IndexOf("<h1>Software error:</h1>")!=-1)
				{
					throw new Exception("Error while trying to do search!");
				}
				StringHash[] core = Bug.xmlParser((string)curr,"bz:bug",mappings, limit);
				foreach(StringHash h in core)
				{
					int id = System.Convert.ToInt32(h["ID"],10);
					Bug b = Bug.getExisting(id);
					if (b == null)
					{
						b = new Bug(id,this.bugz);
						b.values = new StringHash();
						assignKeys(b,h);
						b.setValues();
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
				/*if (bugs.Count==0)
					throw new Exception("no bugs in result!");*/
				//throw new Exception();
				Console.WriteLine("we have {0} bugs",bugs.Count);
			}
			else
				Console.WriteLine("Bug list acquiring error");
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
			foreach(string key in mappings.Keys)
			{
				string oldkey = null;
				if (h.ContainsKey(key))
					oldkey = key;
				else if (h.ContainsKey("bz:"+key))
					oldkey = "bz:"+key;
				if (oldkey!=null)
				{
					h[mappings[key]] = h[oldkey];
					h.Remove(oldkey);
				}
			}
			bool change = true;
			while(change)
			{
				change = false;
				foreach(string key in h.Keys)
				{
					if (key.Length>3 && key.Substring(0,3) == "bz:")
					{
						h[key.Substring(3)] = h[key];
						h.Remove(key);
						//Console.WriteLine("replaced {0} with {1}",key,key.Substring(3));
						change = true;
						break;
					}
				}
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

		public static StringHash[] xmlParser(string input, string separator)
		{
			return xmlParser(input,separator,new StringHash());
		}

		public static StringHash[] xmlParser(string input, string separator, StringHash mappings)
		{
			return xmlParser(input,separator,mappings,-1);
		}

		public static StringHash[] xmlParser(string input, string separator, StringHash mappings, int limit)
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
								else
								{
									ret[element] += "\n\n"+reader.Value;
								}
								break;
						}
						if (reader.Read()==false)
							break;
					}
					rows.Add(ret);
					if (limit!=-1 && rows.Count == limit)
						break;
					else	
						continue;
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
			if (((StringHash)o)["Status"]!="NEEDINFO" && ((StringHash)o)["Status"]!="INCOMPLETE")
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
http://live.gnome.org/GettingTraces for more information on how to do so and
reopen this bug or report a new one. Thanks in advance!";
			orig["knob"] = "resolve";
			orig["resolution"] = "INCOMPLETE";
			bugz.changeBug(orig,new Response(remove,r));
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

		private struct DupeStorage
		{
			public StringHash orig;
			public Bug dupe;
		};

		private void dupidResponse(object curr, object input, Response r)
		{
			DupeStorage ds = (DupeStorage)input;
			setDupeResponse(ds.orig,ds.dupe,r);
		}
		
		private void setDupeResponse(object curr, object input, Response r)
		{
			StringHash orig = (StringHash)curr;
			Bug dupe = (Bug)input;
			if (dupe == null)
				throw new Exception("dupe is null!");
			if (dupe.values == null)
				throw new Exception("dupe.values is null!");
			int dup_id = dupe.id;	
			if (dupe.values["Status"] == "RESOLVED" && dupe.values["resolution"] == "DUPLICATE")
			{
				if (dupe.dupid == -1)
				{
					DupeStorage ds = new DupeStorage();
					ds.orig = orig;
					ds.dupe = dupe;
					dupe.getDupid(new Response(dupidResponse,r,ds));
					return;
				}
				else
					dup_id = dupe.dupid;
			}
			if (dupe.values["Status"] == "NEEDINFO" || (dupe.values["Status"] == "RESOLVED" && dupe.values["resolution"] == "INCOMPLETE"))
				orig["comment"] = "Thanks for taking the time to report this bug.\nThis particular bug has already been reported into our bug tracking system, but the maintainers need more information to fix the bug. Could you please answer the questions in the other report in order to help the developers?";
			else if (dupe.values["Status"] == "RESOLVED" && dupe.values["resolution"] == "FIXED")
				orig["comment"] = "Thanks for taking the time to report this bug.\nThis particular bug has already been reported into our bug tracking system, but we are happy to tell you that the problem has already been fixed. It should be solved in the next software version. You may want to check for a software upgrade.";
			else
				orig["comment"] = "Thanks for the bug report. This particular bug has already been reported into our bug tracking system, but please feel free to report any further bugs you find";
			orig["knob"] = "duplicate";
			orig["resolution"] = "FIXED";
			orig["dup_id"] = String.Concat(dup_id);
			foreach(string s in orig.Keys)
			{
				if (s!="thetext")
					Console.WriteLine("{0} = {1}",s,orig[s]);
			}
			bugz.changeBug(orig,new Response(remove,r));
		}
		
		private void parseInput(Response r)
		{
			parseInput(r,null);
		}

		private void parseInput(Response r, object input)
		{
			getRaw(new Response(parseInputResponse,r,input));
		}

		private void parseInputResponse(object curr, object input, Response chain)
		{
			StringHash orig = Bug.xmlParser((string)curr,"bug",Bug.mappings)[0];
			this.values = (StringHash)orig;
			setValues();

			string[] must = new string[] {"blocked","dup_id","keywords","dependson","newcc","status_whiteboard","bug_file_loc","alias"};
			foreach (string s in must)
			{
				if (!orig.ContainsKey(s))
					orig.Add(s,"");
			}
			orig.Add("form_name","process_bug");
			orig.Add("addselfcc","on");
			orig.Add("longdesclength","1"); // FIXME: assumes one comment!
			orig.Add("knob2","none");
			orig["delta_ts"] = DateTime.ParseExact(orig["delta_ts"], "yyyy-MM-dd HH:mm:ss UTC", null).ToString("yyyy-MM-dd HH:mm:ss");
			foreach(string s in orig.Keys)
			{
				if (s!="thetext")
					Console.WriteLine("pit: {0} = {1}",s,orig[s]);
			}
			Response.invoke(chain,orig);
		}

		public void getDupid(Response r)
		{
			if (dupid!=-1 || values["Status"] != "RESOLVED" || values["resolution"] != "DUPLICATE")
				Response.invoke(r,dupid);
			else
				getRaw(new Response(getDupidWithData,r));
		}

		public void getDupidWithData(object curr, object input, Response chain)
		{
			string raw = (string)curr;
			const string pattern = "\\*\\*\\* This bug has been marked as a duplicate of (\\d+) \\*\\*\\*";
			MatchCollection mc = Regex.Matches(raw, pattern, RegexOptions.Singleline);
			Console.WriteLine("Finding dupe id for {0}",id);
			Match m = mc[mc.Count-1];
			int new_idx = System.Convert.ToInt32(m.Groups[1].Captures[0].Value, 10);
			Console.WriteLine("dupid is {0} for {1}",new_idx,id);
			dupid = new_idx;
			Bug b = getExisting(new_idx);
			if (b != null && b.values!=null)
				dupeParent(b,input,chain);
			else
			{
				b = new Bug(new_idx,bugz);
				b.buildBug(new Response(dupeParent,chain,input));
			}
		}

		public void dupeParent(object curr,object input, Response chain)
		{
			Bug parent = (Bug)curr;
			parent.getDupid(new Response(dupeParentRet,chain,input));
		}
		
		public void dupeParentRet(object curr,object input, Response chain)
		{
			int poss = (int)curr;
			if (poss !=-1)
				dupid = poss;
			if (values!=null)	
				setValues();	
			Response.invoke(chain,dupid);
		}

		public void buildStacktrace(Response r)
		{
			if (this.comments == null)
				buildBug(new Response(buildStacktraceResponse,r));
			else
				buildStacktraceResponse(null,null,r);
		}

		private void buildStacktraceResponse(object curr, object input, Response chain)
		{
			chain.invoke(new Stacktrace(this.id,this.comments));
		}

		public void simpleDupe(Response r)
		{
			bugz.simpleDupe(this.id,r);
		}
	}

	public class Stacktrace
	{
		//const string pattern = "#(\\d+)\\s+(?:0x[\\da-f]+ in <span class=\"trace-function\">([^<]+)</span>\\s+\\([^\\)]*?\\)\\s+(?:at\\s+([^:]+:\\d+)|from\\s+([^ \n\r]+))?|<a name=\"stacktrace\"></a><span class=\"trace-handler\">&lt;(signal handler) called&gt;</span>)"; //(?:(?)|
		const string pattern = "#(\\d+)\\s+(?:(?:0x[\\da-f]+ in )?([^\\s]+)\\s+\\([^\\)]*?\\)[\\s\n]+(?:at\\s+([^:]+:\\d+)|from\\s+()[^ \n\r]+)?|&lt;(signal handler) called&gt;)"; //(?:(?)|
		
		string raw = "";
		public List<string[]> content = null;
		public int id;

		private static string[] worthless = {"__kernel_vsyscall","raise", "abort", "g_free", "memcpy",  "NSGetModule", "??","g_logv","g_log","g_thread_create_full","start_thread","clone","g_type_check_instance_is_a","g_hash_table_insert","g_type_check_instance_cast","g_idle_dispatch","IA__g_main_context_dispatch","g_main_context_iterate","IA__g_main_loop_run","g_closure_invoke","g_signal_emit_valist","gtk_propagate_event","gtk_main_do_event","g_main_context_dispatch","g_main_loop_run","gtk_main","__libc_start_main","waitpid","bonobo_main","main","poll","gtk_widget_show","__cxa_finalize","_fini","gtk_widget_size_request","g_object_unref","_x_config_init","_IO_stdin_used","gettimeofday","__read_nocancel","gtk_main_quit","strlen","gtk_widget_hide","pthread_mutex_lock","strcmp","strrchr","IA__g_log","IA__g_logv","_start","pthread_mutex_unlock","gtk_object_destroy","gtk_widget_destroy","_gdk_events_init","free","kill","g_source_is_destroyed","g_main_context_check","exit","gtk_dialog_run", "memset", "_init"};

		private static string[] single_notuse = {"fm_directory_view_bump_zoom_level","dbus_connection_dispatch","gdk_event_dispatch","gnome_vfs_job_get_count","nautilus_directory_async_state_changed","gtk_container_check_resize","gtk_widget_get_default_style","gtk_button_clicked","gtk_button_released","gdk_window_is_viewable","gtk_container_foreach","gconf_listeners_notify","g_signal_emit","g_source_get_current_time","g_assert_warning","strstr","g_malloc","gst_pad_push","g_signal_emit_by_name","g_timeout_dispatch","g_thread_self"};
		
		bool hasGoodItem()
		{
			for (int i=0;i<this.content.Count;i++)
			{
				if (this.content[i][0]!="" && Array.IndexOf(single_notuse,this.content[i][0])==-1 && this.content[i][0].IndexOf("g_cclosure_marshal")==-1)
				{
					return true;
				}
			}
			return false;
		}

		public Stacktrace(int id, string data)
		{
			this.raw = data;
			this.id = id;
			this.content = new List<string[]>();
			if (data.IndexOf("Traceback (most recent call last):")!=-1 && genPythonStackTrace(data)) {}
			else
				genStackTrace(data);
			if (!hasGoodItem())
			{
				Console.WriteLine("entire trace is useless");
				while (this.content.Count>0)
					this.content.RemoveAt(0);
			}

			/*foreach(string[] s in this.content.ToArray(typeof(string[])))
			{
				Console.WriteLine(s[0]);
			}*/
		}

		private void genStackTrace(string data)
		{
			int limit = 0;
			bool seen_signal = false;
			int idx = -1;

			if (data.IndexOf("(gdb)")!=-1) // manually generated trace, assume there's a signal there
				seen_signal = true;
			if (data.IndexOf("launchpad.net/ubuntu/?source/")!=-1 || data.IndexOf("https://bugs.launchpad.net/bugs/")!=-1) // launchpad import, assume signal
				seen_signal = true;

			foreach (Match m in Regex.Matches(this.raw, Stacktrace.pattern, RegexOptions.Singleline))
			{
				int new_idx = System.Convert.ToInt32(m.Groups[1].Captures[0].Value, 10);
				if (idx!=-1 && new_idx<idx)
				{
					if(seen_signal)
					{
						if (hasGoodItem())
							break;	
						else
							seen_signal = false;
					}
					
					this.content = new List<string[]>();
					limit = 0;
				}
				idx = new_idx;	
				if (m.Groups[2].Captures.Count!=0)
				{
					bool usable = true;
					string[] tostore = new string[2];
					tostore[0] = m.Groups[2].Captures[0].Value;
					if (m.Groups[3].Captures.Count!=0)
						tostore[1] = m.Groups[3].Captures[0].Value;
					else if (m.Groups[4].Captures.Count!=0)
						tostore[1] = m.Groups[4].Captures[0].Value;
					else
						tostore[1] = "";
					if (tostore[1].IndexOf("/")!=-1)
					{
						tostore[1] = tostore[1].Substring(tostore[1].LastIndexOf("/")+1);
					}

					if (tostore[0].IndexOf("_ORBIT_skel_small_")==0)
						tostore[0] = tostore[0].Substring("_ORBIT_skel_small_".Length);

					//Console.WriteLine("tostore: {0} {1}",tostore[0],tostore[1]);

					if (tostore[0].IndexOf("POA")==0 || tostore[0].IndexOf("_dl_")==0|| Array.IndexOf(worthless,tostore[0])!=-1 || tostore[0].IndexOf("*")!=-1 || (tostore[0].IndexOf("__")!=-1 && this.content.Count<2))
					{
						usable = false;
						tostore[0] = "";
					}
					//	continue;

					if (seen_signal)
					{
						if (usable)
						{
							this.content.Add(tostore);
							if (limit>0 && this.content[limit-1][0] == tostore[0])
							{
								Console.WriteLine("duplicate line {0}",tostore[0]);
								continue;
							}
							limit++;
							if (limit==5)
								break;
						}
						else if (this.content.Count>0) // only add crappy entries after at least one good one
							this.content.Add(tostore);
					}
				}
				else if (m.Groups[5].Captures.Count!=0)
				{
					Console.WriteLine(" signal handler!");
					Console.WriteLine(m.Groups[5].Captures[0].Value);
					seen_signal = true;
				}
			}

		}

		const string trace = "Traceback \\(most recent call last\\):(.+)";
		const string pythonPattern = "File &quot;([^&]+)&quot;, line (\\d+), in\\s+([^\\\n]+)";
		const string pythonExcept = "(.+(?:Exception|Error|error): .+)"; 

		private bool genPythonStackTrace(string data)
		{
			Match m2 = Regex.Match(this.raw, Stacktrace.trace, RegexOptions.Singleline);
			Console.WriteLine("stuff: {0}",m2.Groups[1].Captures[0].Value);
			string tr = m2.Groups[1].Captures[0].Value;
			
			foreach (Match m in Regex.Matches(tr, Stacktrace.pythonPattern, RegexOptions.Singleline))
			{
				Console.WriteLine("stuff: {0}, {1}, {2}",Path.GetFileName(m.Groups[1].Captures[0].Value),m.Groups[2],m.Groups[3]);
				//this.content.Add(new string[] {Path.GetFileName(m.Groups[1].Captures[0].Value),m.Groups[2].Captures[0].Value,m.Groups[3].Captures[0].Value});
				if (m.Groups[1].Captures.Count == 0 || m.Groups[3].Captures.Count == 0)
					goto clear_content;
				string val = m.Groups[3].Captures[0].Value;
				if (val == "&lt;lambda&gt;")
					continue;
				this.content.Add(new string[] {Path.GetFileName(m.Groups[1].Captures[0].Value),val});
			}

			m2 = Regex.Match(tr,Stacktrace.pythonExcept);
			
			if (m2.Groups[1].Captures.Count == 0)
				goto clear_content;
			string s = m2.Groups[1].Captures[0].Value;
			List<string> bits = new List<string>(s.Split(':'));
			s = "";
			while (bits.Count>1)
			{
				if (bits[0].Trim().IndexOf(" ")!=-1 || !Char.IsLetterOrDigit(bits[0].Trim()[0]))
					break;
				if (s!="")
					s+= ":";
				s += bits[0];
				bits.RemoveAt(0);
			}
			if (s=="")
				throw new Exception();
			Console.WriteLine("s = '{0}'",s);
			this.content.Add(new string[]{s});

			return true;

			clear_content:
			Console.WriteLine("Failure, clearing!");
			this.content.Clear();
			return false;
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
			int smallest = this.content.Count<right.content.Count?this.content.Count:right.content.Count;
			if (smallest == 0)
				return false;
			for(int idx = 0;idx<smallest;idx++)
			{
				string [] one = this.content[idx];
				string [] two = right.content[idx];
				if (one.Length!=two.Length)
					return false;
				for(int j=0;j<one.Length;j++)
				{
					Console.WriteLine("comparing {0} and {1}",one[j],two[j]);
					if (one[j] == ""|| two[j] == "") // "" == junk, which matches all
						break;
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
				StringBuilder sb = new StringBuilder(s[0]);
				for (int i=1;i<s.Length;i++)
				{
					sb.Append(":");
					sb.Append(s[i]);
				}
				ret.Append(sb.ToString());
			}
			return ret.ToString();
		}

		public bool usable()
		{
			if (this.content.Count == 0)
				return false;
			if (this.content[0][0] == "poll" && this.content.Count>1 && this.content[1][0] == "g_main_context_check") // evolution bugs that we're hacking around
				return false;
			return true;
		}

		public void print()
		{
			Console.WriteLine("Stacktrace for {0}",this.id);
			foreach(string[] s in this.content)
			{
				foreach (string b in s)
					Console.Write("{0} ",b);
				Console.Write("\n");	
			}
		}

	}

	public class Bugzilla
	{
		string root = "";
		string cachepath = null;
		
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
			cachepath = Path.GetFullPath("cache");
			if (cachepath[cachepath.Length-1] != Path.DirectorySeparatorChar)
				cachepath += Path.DirectorySeparatorChar;
			Console.WriteLine("cachepath {0}",cachepath);
			wp = null;
			FileInfo f=new FileInfo("cookies.dat");
			if (f.Exists)
			{
				Stream s=f.Open(FileMode.Open);
				BinaryFormatter b=new BinaryFormatter();
				cookies = (CookieContainer)b.Deserialize(s);
				s.Close();
				HttpWebRequest myRequest = genRequest("index.cgi");
				HttpWebResponse wre = (HttpWebResponse) myRequest.GetResponse();
				SafeStreamReader sr = new SafeStreamReader(wre.GetResponseStream(), Encoding.ASCII);
				string ret = "";
				try
				{
					ret = sr.ReadToEnd();
				}
				catch
				{
					Console.WriteLine("Exception while reading login");
				}
				sr.Close();
				TextWriter outFile = new StreamWriter("login-test");
				outFile.Write(ret);
				outFile.Close();
				if (ret.IndexOf("Logged In")!=-1)
					_loggedIn = true;
				else
					cookies = null;
			}
		}

		private HttpWebRequest genRequest(string path)
		{
			HttpWebRequest myRequest = (HttpWebRequest)WebRequest.Create(root+path);
			myRequest.UserAgent = "Pyro bug triager/0.2";
			myRequest.Timeout = 20*1000; // 20 seconds
			if (wp!=null)
				myRequest.Proxy = wp;
			myRequest.CookieContainer = cookies;
			return myRequest;
		}

		public bool login(string username, string password)
		{
			if (cookies==null || cookies.GetCookieHeader(new System.Uri(root)).IndexOf("Bugzilla_login")==-1)
			{
				string postData="Bugzilla_login="+username+"&Bugzilla_password="+password+"&Bugzilla_remember=on&Bugzilla_restrictlogin=on&GoAheadAndLogIn=1&GoAheadAndLogIn=Log+in";
				ASCIIEncoding encoding=new ASCIIEncoding();
				byte[] data = encoding.GetBytes(postData);

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
				SafeStreamReader sr = new SafeStreamReader(wre.GetResponseStream(), Encoding.ASCII);
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
			else
			{
				_loggedIn = true;
				return true;
			}
		}


		private struct getDataState
		{
			public HttpWebRequest req;
			public string path;
		}
		
		/*public static string strip(string inVal)
		{
			string val = Regex.Replace(inVal,"<script type=\"text/javascript\">.*?</script>","",RegexOptions.Singleline);
			val = Regex.Replace(val,"<link href=\"skins/standard/global.css\" rel=\"stylesheet\" type=\"text/css\">","<style type=\"text/css\">\n.trace-function { color: #cc0000; }\n.trace-handler  { color: #4e9a06; }\n</style>");

			string pattern = @"</?(?i:img|base|script|embed|object|frameset|frame|iframe|meta|link)(.|\n)*?>";
			return Regex.Replace(val, pattern, "").Trim();
		}*/

		private static char[] valids = {'.','-'};

		private string path(string cache)
		{
			string ret = Path.GetFullPath(Path.Combine(cachepath,cache));
			char[] data = ret.ToCharArray();
			for (int i=cachepath.Length;i<data.Length;i++)
			{
				if (!Char.IsLetterOrDigit(data[i]) && Array.IndexOf(valids,data[i])==-1)
					data[i] = '-';
			}
			ret = new String(data);
			if (ret.Length>256)
				return ret.Substring(0,256);
			else
				return ret;
		}

		private bool hasData(string cache)
		{
			return new FileInfo(path(cache)).Exists;
		}

		private double age(string cache)
		{
			return (DateTime.Now - new FileInfo(path(cache)).LastWriteTime).TotalSeconds;
		}

		public bool isTooOld(string cache, double max_age)
		{
			if (!hasData(cache))
				return true;
			if (max_age == -1)
				return false;
			return age(cache)>max_age;
		}

		public string readData(string cache)
		{
			SafeStreamReader inFile = new SafeStreamReader(path(cache));
			string ret = inFile.ReadToEnd();
			if (ret.IndexOf("Ill-formed Query")!=-1)
				throw new Exception("Bad query! "+path(cache));
			inFile.Close();
			return ret;
		}

		private void getData(string url, string cache, Response r) {getData(url,cache,-1,r);}
		private void getData(string url, string cache, double max_age, Response chain)
		{
			Console.WriteLine("grabbing {1}{0} ({2})",url,root, cache);
			if (isTooOld(cache,max_age))
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
			StreamWriter outFile = new StreamWriter(path,false,Encoding.UTF8);
			Console.WriteLine("Writing {0}",path);
			outFile.Write(new SafeStringReader(data).ReadToEnd());
			outFile.Close();
		}

		private void getDataCallback(IAsyncResult ar)
		{
			Response r = (Response)ar.AsyncState;
			try
			{
				string ret = "";
				getDataState st = (getDataState)r.input;
				HttpWebResponse wre = (HttpWebResponse) st.req.EndGetResponse(ar);
				SafeStreamReader sr = new SafeStreamReader(wre.GetResponseStream()); /*, Encoding.UTF8);*/
				Console.WriteLine("\nResponse for {0}\n",st.path);
				ret = sr.ReadToEnd();
				sr.Close();
				if (!Directory.Exists(cachepath))
				{
					Directory.CreateDirectory(cachepath);
				}
				Console.WriteLine("Got {0}",st.path);
				writePath(st.path,ret);
				if (ret.IndexOf("Ill-formed Query")!=-1)
					throw new Exception("Bad query! "+st.path);
				Response.invoke(r,ret);
			}
			catch (Exception e)
			{
				Console.WriteLine("async exception");
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
				/*Response.invoke(r,null);*/
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
			public double max_age;
		}
		
		public void getBug(int id, Response r) { getBug(new int[]{id},r);}
		public void getBug(int[] id, Response r) { getBug(id,-1, r);}

		public void getBug(int[] id, double max_age, Response r)
		{
			BugList bl = new BugList();
			bl.complete = id;
			bl.todo = new Stack<int>(id);
			bl.max_age = max_age;
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
				if(isTooOld(bugPath(x),bl.max_age))
				{
					grab.AppendFormat("&id={0}",x);
					path.AppendFormat("-{0}",x);
					find = true;
					if (path.Length>150) /* sensible limit given varying path limits */
						break;
				}
			}
			if (find)
				getData(grab.ToString(), path.ToString(), bl.max_age, new Response(splitBugs,r,bl));
			else
				splitBugs(null,bl,r);
		}

		private void splitBugs(object res, object input, Response r)
		{
			BugList bl = (BugList)input;
			if (res!=null)
			{
				bool found = false;
				NonValidatingReader reader = new NonValidatingReader((string)res);
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
							BugDB.DB.setExisting(id);
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
				Console.Write("Splitbugs list: ");
				foreach (int x in bl.complete)
				{
					ret.Append(readData(bugPath(x)));
					Console.Write("{0} ",x);
				}
				Console.WriteLine("");
				Response.invoke(r,ret.ToString());	
			}
		}

		public void simpleDupe(int id, Response r)
		{
			getData("dupfinder/simple-dup-finder.cgi?id="+id, String.Concat(id)+Bug.sd, r);
		}

		public void product(string name, Response r)
		{
			getData("buglist.cgi?query=product%3A"+name.Replace("+","%2B")+"+responders%3A0+status%3Aunconfirmed+severity%3Acritical+priority%3Ahigh&ctype=rdf",name,24*60*60,r);
		}

		public void numbered(int id, int id2, Response r)
		{
			if (id == id2)
				getData(String.Format("buglist.cgi?query=bug-number%3D{0}+meta-status%3Aall&ctype=rdf",id,id2),"numbered",0,r);
			else
				getData(String.Format("buglist.cgi?query=bug-number%3E%3D{0}+bug-number%3C%3D+meta-status%3Aall{1}&ctype=rdf",id,id2),"numbered",0,r);
		}

		public void corebugs(Response r)
		{
			getData("buglist.cgi?query_format=advanced&short_desc_type=allwordssubstr&short_desc=crash&long_desc_type=allwordssubstr&long_desc=&status_whiteboard_type=allwordssubstr&status_whiteboard=&keywords_type=anywords&keywords=&bug_status=UNCONFIRMED&bug_severity=blocker&bug_severity=critical&bug_severity=major&priority=Urgent&priority=High&priority=Normal&emailtype1=substring&email1=&emailtype2=substring&email2=&bugidtype=include&bug_id=&chfieldfrom=1w&chfieldto=Now&chfield=[Bug+creation]&chfieldvalue=&cmdtype=doit&order=Reuse+same+sort+as+last+time&field0-0-0=noop&type0-0-0=noop&value0-0-0=&format=rdf","week-bugs", 60*60, r);
		}

		public void stackDupe(Stacktrace st, Response r)
		{
			StringBuilder query = new StringBuilder("meta-status:all");
			StringBuilder name = new StringBuilder("stackdupe");
			List<string> added = new List<string>();

			//FIXME: workaround for http://bugzilla.gnome.org/show_bug.cgi?id=467015
			added.Add("bus.py");

			foreach(string[] strs in st.content)
			{
				foreach(string s in strs)
				{
					if (s!="" && !added.Contains(s) && s.Length>3)
					{
						Console.WriteLine("s is '{0}'",s);
						query.Append(" \""+s.Replace("&apos;","\\\'").Replace("&lt;","<").Replace("&gt;",">")+"\"");
						name.Append("-"+s.Replace("(","_").Replace(")","_"));
						added.Add(s);
					}
				}
			}
			ASCIIEncoding encoding=new ASCIIEncoding();
			encoding.GetBytes(query.ToString());
			getData("buglist.cgi?ctype=rdf&order=bugs.bug_status,bugs.bug_id&query="+System.Web.HttpUtility.UrlEncode(encoding.GetBytes(query.ToString())),name.ToString(),r);
		}

		private struct changeState
		{
			public HttpWebRequest req;
			public string query;
			public byte[] data;
		}

		public void changeBug(StringHash values, Response chain)
		{
			StringBuilder query = new StringBuilder();
			string [] dont = {"thetext","Status","bug_when"};
			foreach(string s in values.Keys)
			{
				if (Array.IndexOf(dont,s)!=-1)
					continue;
				Console.WriteLine("{0} = {1}",s,values[s].Replace("&#64;","%40"));
				if (query.Length!=0)
					query.Append("&");
				query.AppendFormat("{0}={1}",s,values[s].Replace("&#64;","%40"));
			}
			int id = Int32.Parse(values["id"]);
			if (BugDB.DB.getExisting(id)==null)
			{
				TextWriter outFile = new StreamWriter("change-test");
				outFile.Write(query);
				outFile.Close();
				throw new Exception("bug doesn't exist!");
			}

			ASCIIEncoding encoding=new ASCIIEncoding();

			changeState state = new changeState();
			state.data = encoding.GetBytes(query.ToString());

			state.query = query.ToString();
			state.req = genRequest("process_bug.cgi");
			state.req.Method = "POST";
			state.req.ContentType="application/x-www-form-urlencoded";
			state.req.ContentLength = state.data.Length;
			state.req.CookieContainer = cookies;

			Console.WriteLine("Doing bug update");
			state.req.BeginGetRequestStream(new AsyncCallback(changeCallback),new Response(getDataShim,chain,state));
		}

		private void changeCallback(IAsyncResult ar)
		{
			Response r = (Response)ar.AsyncState;
			try
			{
				changeState st = (changeState)r.input;
				Stream newStream=st.req.EndGetRequestStream(ar);
				newStream.Write(st.data,0,st.data.Length);
				newStream.Close();
				HttpWebResponse wre = (HttpWebResponse) st.req.GetResponse();
				//HttpWebResponse wre = (HttpWebResponse) st.req.EndGetResponse(ar);
				SafeStreamReader sr = new SafeStreamReader(wre.GetResponseStream());

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
				outFile.Write(st.query);
				outFile.Write("\n\n");
				outFile.Write(ret);
				outFile.Close();
				if (ret.IndexOf("<title>Log in to Bugzilla</title>")>0)
				{
					throw new Exception("Panic! Not logged in");
				}
				Console.WriteLine("Bug update complete");
				Response.invoke(r,false);
			}
			catch (Exception e)
			{
				Console.WriteLine("async exception");
				Console.WriteLine(e.Message);
				Console.WriteLine(e.StackTrace);
				/*Response.invoke(r,null);*/
				Environment.Exit(1);
				throw e;
			}
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
			dbcon = new SqliteConnection("URI=file:bugs.db,version=3,busy_timeout=100");
			dbcon.Open();
			IDbCommand dbcmd = new SqliteCommand("select name from sqlite_master where type='table' and name='bugs'",dbcon);
			IDataReader reader = dbcmd.ExecuteReader();
			if (!reader.Read())
			{
				dbcmd = new SqliteCommand("create table bugs(id integer primary key, done boolean, Status text, Priority text, Severity Text, stackhash text, Resolution Text, dupid integer)",dbcon);
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
			if (st.old == null)
				throw new Exception("should have an old stacktrace");
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
			dbcmd.CommandText = "select id from bugs where stackhash=@hash and id<"+String.Concat(st.old.id)+" order by id";
			dbcmd.Parameters.Add(new SqliteParameter("@hash",hash));	
			IDataReader reader = dbcmd.ExecuteReader();
			if (reader.Read())
			{
				Bug b = getExisting(reader.GetInt32(0));
				if (b.id != 0 && b.stackhash!="")
				{
					if (b.stackhash != hash)
					{
						Console.WriteLine("Old stackhash: '{0}'",hash);
						Console.WriteLine("bug stackhash: '{0}'",b.stackhash);
						b.stackhash = hash;
						Console.WriteLine("\nDB ERROR!\n");
					}
					if (!File.Exists(b.localpath()))
						throw new Exception("Can't find bug "+String.Concat(b.id));
					if (b.values["Status"] == "RESOLVED" && b.values["resolution"] == "DUPLICATE" && b.dupid==-1)
					{
						//throw new Exception("dupes is a dupe, but no dupid!");
					}
					else
					{
						Response.invoke(r,b);
						return;
					}
				}
			}
			Console.WriteLine("no dupe found in existing db");
			dbcmd.CommandText = "select id from bugs where stackhash=\"\" and id<"+String.Concat(st.old.id)+" order by id";
			reader = dbcmd.ExecuteReader();
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

		public void setValues(Bug b)
		{
			if (getExisting(b.id)==null)
				setExisting(b.id);
			IDbCommand dbcmd = dbcon.CreateCommand();
			if (b.values == null)
			{
				dbcmd.CommandText = "update bugs set Status=\"\",Priority=\"\",Severity=\"\"";
				throw new Exception("no values!");
			}
			else
			{
				dbcmd.CommandText = "update bugs set Status=\""+b.values["Status"]+"\"";
				if (b.values.ContainsKey("priority"))
					dbcmd.CommandText += ",Priority=\""+b.values["priority"]+"\"";
				if (b.values.ContainsKey("bug_severity"))
					dbcmd.CommandText += ",Severity=\""+b.values["bug_severity"]+"\"";
				if (b.values.ContainsKey("resolution"))
					dbcmd.CommandText += ",Resolution=\""+b.values["resolution"]+"\"";
				dbcmd.CommandText += ",dupid="+String.Concat(b.dupid);
			}
			dbcmd.CommandText += " where id="+String.Concat(b.id);
			//throw new Exception(dbcmd.CommandText);
			int ret = dbcmd.ExecuteNonQuery();
			if (ret!=1)
			{
				throw new Exception(String.Format("rows affected: {0}",ret));
			}
		}
		
		public void setStackHash(int id, Stacktrace st)
		{
			setStackHash(id,st.getHash());
		}

		public void setStackHash(int id, string st)
		{
			setExisting(id);
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "update bugs set stackhash=@hash where id="+String.Concat(id);
			dbcmd.Parameters.Add(new SqliteParameter("@hash",st));	
			dbcmd.ExecuteNonQuery();
		}

		public void setDone(int id)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "update bugs set done=1 where id="+String.Concat(id);
			dbcmd.ExecuteNonQuery();
		}

		public void clearDone(int id)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "update bugs set done=0 where id="+String.Concat(id);
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
					Bug b = getExisting(st2.id);
					if (b.stackhash != st.oldst.getHash())
					{
						Console.WriteLine("{0} != {1}",b.stackhash,st.oldst.getHash());
						throw new Exception("db error");
					}
					if (!File.Exists(b.localpath()))
						throw new Exception("Can't find bug "+String.Concat(b.id));
					/*if (b.values["Status"] == "RESOLVED" && b.values["resolution"] == "DUPLICATE")
						b.getDupid();*/
					Response.invoke(r,b);
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
			dbcmd.CommandText = "select Severity,Priority,Status,stackhash,Resolution from bugs where id="+String.Concat(id);
			IDataReader reader = dbcmd.ExecuteReader();
			if (reader.Read())
			{
				Bug ret = new Bug(id,bugz);
				bugs[id] = ret;
				if (!reader.IsDBNull(0))
					ret.setValue("bug_severity",reader.GetString(0));
				if (!reader.IsDBNull(1))
					ret.setValue("priority",reader.GetString(1));
				if (!reader.IsDBNull(2))
					ret.setValue("Status",reader.GetString(2));
				if (!reader.IsDBNull(3))
					ret.stackhash = reader.GetString(3);
				if (!reader.IsDBNull(4))
					ret.setValue("resolution",reader.GetString(4));
				reader.Close();
				return ret;
			}
			else
			{
				reader.Close();
				dbcmd.Cancel();
				return null;
			}
		}

		public void remove(int id)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "delete from bugs where id=@id";
			dbcmd.Parameters.Add(new SqliteParameter("@id",id));	
			dbcmd.ExecuteNonQuery();
		}

		public void setExisting(int id)
		{
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "select done from bugs where id="+String.Concat(id);
			IDataReader reader = dbcmd.ExecuteReader();

			if (!reader.Read())
			{
				reader.Close();
				dbcmd.Cancel();
				IDbCommand dbcmd2 = dbcon.CreateCommand();
				dbcmd2.CommandText = "insert into bugs (id,done) values(@id,0)";
				dbcmd2.Parameters.Add(new SqliteParameter("@id",id));	
				dbcmd2.ExecuteNonQuery();
			}
		}

		public int[] allBugs()
		{
			List <int> ret = new List<int>();
			IDbCommand dbcmd = dbcon.CreateCommand();
			dbcmd.CommandText = "select id from bugs";
			IDataReader reader = dbcmd.ExecuteReader();
			while (reader.Read())
			{
				ret.Add(reader.GetInt32(0));
			}
			return ret.ToArray();
		}
	}
}
