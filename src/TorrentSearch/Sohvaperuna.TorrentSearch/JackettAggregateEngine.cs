using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;
using System.Xml;

namespace Sohvaperuna.TorrentSearch
{
	/// <summary>
	/// Talks to a Jackett server and exposes each configured indexer as a search engine,
	/// plus one "Jackett (all indexers)" engine that searches everything in a single request.
	/// Settings come from the &lt;jackett&gt; block in MyTorrents.xml (url, apikey, enabled).
	/// 2026: rewritten - configurable address, API key, timeouts, no crash when Jackett is not running.
	/// </summary>
	public class JackettAggregateEngine
	{
		public const string AllIndexersName = "Jackett (all indexers)";

		public List<ITorrentSearch> Engines = new List<ITorrentSearch>();

		private readonly JavaScriptSerializer js = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

		public static jackettSettings jSettings = new jackettSettings();

		/// Base API url, always ending in "/api/v2.0/"
		public static string apiUrl = "http://127.0.0.1:9117/api/v2.0/";

		/// 2026: how many sites Jackett searches (-1 = not known yet)
		public static int IndexerCount = -1;

		/// 2026: the sites Jackett searches - "all indexers" asks them one by one, in parallel (see JackettSearchProvider)
		public static List<JackettEngine> ConfiguredIndexers = new List<JackettEngine>();

		public static bool Enabled = true;

		/// Result of the last connection attempt, for the settings "Test" button:
		/// "ok", "nokey", "badkey", "unreachable" (with LastError).
		public static string LastStatus = "";

		public static string LastError = "";

		/// Reads the optional &lt;jackett&gt; element from MyTorrents.xml.
		public static void Configure(XmlDocument config)
		{
			XmlNode node = config?.SelectSingleNode("config/jackett");
			if (node == null)
			{
				return;
			}
			string url = node.SelectSingleNode("url")?.InnerText?.Trim();
			string key = node.SelectSingleNode("apikey")?.InnerText?.Trim();
			string enabled = node.SelectSingleNode("enabled")?.InnerText?.Trim();
			Enabled = !string.Equals(enabled, "false", StringComparison.OrdinalIgnoreCase);
			if (!string.IsNullOrEmpty(url))
			{
				url = url.TrimEnd('/');
				if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
				{
					url = "http://" + url;
				}
				if (!url.EndsWith("/api/v2.0", StringComparison.OrdinalIgnoreCase))
				{
					url += "/api/v2.0";
				}
				apiUrl = url + "/";
			}
			if (!string.IsNullOrEmpty(key))
			{
				jSettings = new jackettSettings { api_key = key };
			}
		}

		public JackettAggregateEngine()
		{
			LastStatus = "";
			LastError = "";
			if (!Enabled)
			{
				return;
			}
			try
			{
				if (jSettings == null || string.IsNullOrEmpty(jSettings.api_key))
				{
					LoadSettings();
				}
				if (jSettings == null || string.IsNullOrEmpty(jSettings.api_key))
				{
					Log.Instance().Print("Jackett: no API key - add it to the <jackett> section of MyTorrents.xml");
					LastStatus = "nokey";
					return;
				}
				Engines = GetEngines() ?? new List<ITorrentSearch>();
				IndexerCount = Engines.Count - (Engines.Count > 0 ? 1 : 0);
				Log.Instance().Print($"Jackett: {IndexerCount} indexers found at {apiUrl}");
			}
			catch (Exception ex)
			{
				// Jackett not installed / not running - just carry on without it
				Log.Instance().Print("Jackett not available at " + apiUrl + ": " + ex.Message);
				Engines = new List<ITorrentSearch>();
				if (LastStatus != "badkey")
				{
					LastStatus = "unreachable";
					LastError = ex.Message;
				}
			}
		}

		public static string BaseRequest(string url)
		{
			return BaseRequest(url, 60000);
		}

		public static string BaseRequest(string url, int timeoutMs)
		{
			ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
			HttpWebRequest httpWebRequest = WebRequest.Create(url) as HttpWebRequest;
			httpWebRequest.Method = "GET";
			httpWebRequest.Accept = "*/*";
			httpWebRequest.Timeout = timeoutMs;
			httpWebRequest.ReadWriteTimeout = timeoutMs;
			httpWebRequest.UserAgent = "MyTorrents/1.0 (MediaPortal)";
			httpWebRequest.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
			using (WebResponse response = httpWebRequest.GetResponse())
			using (StreamReader streamReader = new StreamReader(response.GetResponseStream()))
			{
				return streamReader.ReadToEnd();
			}
		}

		private List<ITorrentSearch> GetEngines()
		{
			List<JackettEngine> indexers = GetIndexersTorznab() ?? GetIndexersJson();
			if (indexers == null)
			{
				return null;
			}
			List<ITorrentSearch> list = new List<ITorrentSearch>();
			List<Category> allCaps = new List<Category>();
			ConfiguredIndexers = indexers.Where(i => i.configured && !string.IsNullOrEmpty(i.id)).ToList();
			foreach (JackettEngine item in indexers)
			{
				if (item.configured)
				{
					list.Add(new JackettSearchProvider { Enabled = "true", Name = item.name, Info = item });
					foreach (Category c in item.caps ?? new List<Category>())
					{
						if (!allCaps.Exists(a => a.ID == c.ID))
						{
							allCaps.Add(c);
						}
					}
				}
			}
			if (list.Count > 0)
			{
				JackettEngine all = new JackettEngine { id = "all", name = AllIndexersName, configured = true, caps = allCaps, site_link = "" };
				list.Insert(0, new JackettSearchProvider { Enabled = "true", Name = AllIndexersName, Info = all });
			}
			return list;
		}

		/// Indexer list via the Torznab API - works with just the API key, even when Jackett has an admin password.
		private List<JackettEngine> GetIndexersTorznab()
		{
			try
			{
				string xml = BaseRequest($"{apiUrl}indexers/all/results/torznab/api?apikey={Uri.EscapeDataString(jSettings.api_key)}&t=indexers&configured=true", 10000);
				XmlDocument doc = new XmlDocument();
				doc.LoadXml(xml);
				XmlNode error = doc.SelectSingleNode("error");
				if (error != null)
				{
					// e.g. <error code="100" description="Invalid API Key" />
					LastStatus = "badkey";
					LastError = error.Attributes?["description"]?.Value ?? "error";
					throw new Exception("Jackett says: " + LastError);
				}
				LastStatus = "ok";
				List<JackettEngine> result = new List<JackettEngine>();
				foreach (XmlNode n in doc.SelectNodes("indexers/indexer"))
				{
					JackettEngine e = new JackettEngine
					{
						id = n.Attributes?["id"]?.Value,
						configured = n.Attributes?["configured"]?.Value != "false",
						name = n.SelectSingleNode("title")?.InnerText,
						description = n.SelectSingleNode("description")?.InnerText,
						site_link = n.SelectSingleNode("link")?.InnerText,
						language = n.SelectSingleNode("language")?.InnerText,
						type = n.SelectSingleNode("type")?.InnerText,
						caps = new List<Category>()
					};
					foreach (XmlNode c in n.SelectNodes("caps/categories//category | caps/categories//subcat"))
					{
						e.caps.Add(new Category { ID = c.Attributes?["id"]?.Value, Name = c.Attributes?["name"]?.Value });
					}
					if (!string.IsNullOrEmpty(e.id))
					{
						result.Add(e);
					}
				}
				return result;
			}
			catch (Exception ex)
			{
				HttpWebResponse r = (ex as WebException)?.Response as HttpWebResponse;
				if (r != null && (r.StatusCode == HttpStatusCode.Unauthorized || r.StatusCode == HttpStatusCode.Forbidden))
				{
					LastStatus = "badkey";
					LastError = ex.Message;
				}
				Log.Instance().Print("Jackett: torznab indexer list failed (" + ex.Message + "), trying JSON list");
				return null;
			}
		}

		/// Older method (Jackett UI API). Only works when Jackett has no admin password.
		private List<JackettEngine> GetIndexersJson()
		{
			string text = BaseRequest(apiUrl + "indexers?configured=true", 10000);
			return string.IsNullOrEmpty(text) ? null : js.Deserialize<List<JackettEngine>>(text);
		}

		/// Fallback when no API key is configured: read it from Jackett (only works without an admin password).
		private void LoadSettings()
		{
			try
			{
				string text = BaseRequest(apiUrl + "server/config", 5000);
				if (!string.IsNullOrEmpty(text) && text.TrimStart().StartsWith("{"))
				{
					jSettings = js.Deserialize<jackettSettings>(text);
				}
			}
			catch (Exception ex)
			{
				Log.Instance().Print("Jackett: could not read API key automatically: " + ex.Message);
			}
		}
	}
}
