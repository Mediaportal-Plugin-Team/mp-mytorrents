using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;

namespace Sohvaperuna.TorrentSearch
{
	public class TorrentSearchEngine
	{
		private List<ITorrentSearch> _engines;

		public List<ITorrentSearch> Engines => _engines;

		public TorrentSearchEngine(string configFile)
		{
			_engines = new List<ITorrentSearch>();
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(configFile);
			JackettAggregateEngine.Configure(xmlDocument);
			JackettAggregateEngine jackettAggregateEngine = new JackettAggregateEngine();
			if (jackettAggregateEngine.Engines != null)
			{
				foreach (ITorrentSearch engine in jackettAggregateEngine.Engines)
				{
					_engines.Add(engine);
				}
			}
			// Sites added by the user (name + search address) - see SimpleSiteSearchProvider
			foreach (SimpleSiteSearchProvider site in SimpleSiteSearchProvider.LoadAll(xmlDocument))
			{
				_engines.Add(site);
			}
			foreach (XmlNode item in xmlDocument.SelectNodes("config/api"))
			{
				XmlNode xmlNode2 = item.SelectSingleNode("name");
				if (xmlNode2.InnerText == "HDBits API")
				{
					ITorrentSearch torrentSearch = new HDBitsSearchProvider();
					torrentSearch.Parameters["Name"] = xmlNode2.InnerText;
					XmlNode xmlNode3 = item.SelectSingleNode("username");
					torrentSearch.Parameters["username"] = xmlNode3.InnerText;
					XmlNode xmlNode4 = item.SelectSingleNode("passkey");
					torrentSearch.Parameters["passkey"] = xmlNode4.InnerText;
					XmlNode xmlNode5 = item.SelectSingleNode("enabled");
					torrentSearch.Enabled = xmlNode5.InnerText;
					_engines.Add(torrentSearch);
				}
				if (xmlNode2.InnerText == "BTN API")
				{
					ITorrentSearch torrentSearch = new BTNSearchProvider();
					torrentSearch.Parameters["Name"] = xmlNode2.InnerText;
					XmlNode xmlNode4 = item.SelectSingleNode("passkey");
					torrentSearch.Parameters["passkey"] = xmlNode4.InnerText;
					XmlNode xmlNode5 = item.SelectSingleNode("enabled");
					torrentSearch.Enabled = xmlNode5.InnerText;
					_engines.Add(torrentSearch);
				}
			}
			foreach (XmlNode item2 in xmlDocument.SelectNodes("config/searchengines/engine"))
			{
				string innerText = item2.SelectSingleNode("name").InnerText;
				string innerText2 = item2.SelectSingleNode("enabled").InnerText;
				XmlNode xmlNode8;
				if (innerText.Substring(0, 1) == "@")
				{
					TorrentSearch_Combiner torrentSearch_Combiner = new TorrentSearch_Combiner(innerText.Substring(1))
					{
						Enabled = innerText2
					};
					foreach (XmlNode item3 in item2.SelectNodes("addengine"))
					{
						foreach (ITorrentSearch engine2 in _engines)
						{
							if (engine2.Name == item3.InnerText)
							{
								torrentSearch_Combiner.Engines.Add(engine2);
								break;
							}
						}
					}
					foreach (XmlNode item4 in item2.SelectNodes("sort"))
					{
						xmlNode8 = item4.SelectSingleNode("name");
						XmlNode xmlNode9 = item4.SelectSingleNode("itype");
						XmlNode xmlNode10 = item4.SelectSingleNode("iname");
						torrentSearch_Combiner.AddSort(xmlNode8.InnerText, xmlNode9.InnerText, xmlNode10.InnerText);
					}
					_engines.Add(torrentSearch_Combiner);
					continue;
				}
				TorrentSearch_Generic torrentSearch_Generic = new TorrentSearch_Generic(innerText);
				torrentSearch_Generic.Enabled = innerText2;
				torrentSearch_Generic.Parameters["Tracker"] = item2.SelectSingleNode("tracker").InnerText;
				torrentSearch_Generic.Parameters["Parse"] = item2.SelectSingleNode("parse").InnerText;
				torrentSearch_Generic.Parameters["Options"] = item2.SelectSingleNode("options").InnerText;
				torrentSearch_Generic.Parameters["Pages"] = item2.SelectSingleNode("pages").InnerText;
				torrentSearch_Generic.Parameters["Download"] = item2.SelectSingleNode("download").InnerText;
				torrentSearch_Generic.Parameters["DateFormat"] = item2.SelectSingleNode("dateformat").InnerText;
				torrentSearch_Generic.Parameters["Cookie"] = item2.SelectSingleNode("cookie").InnerText;
				torrentSearch_Generic.Parameters["CodePage"] = item2.SelectSingleNode("CodePage").InnerText;
				torrentSearch_Generic.Parameters["Magnet"] = item2.SelectSingleNode("Magnet").InnerText;
				xmlNode8 = item2.SelectSingleNode("parsedate");
				if (xmlNode8 != null)
				{
					torrentSearch_Generic.Parameters["ParseDate"] = xmlNode8.InnerText;
				}
				foreach (XmlNode item5 in xmlDocument.SelectNodes("config/tracker"))
				{
					XmlNode xmlNode2 = item5.SelectSingleNode("name");
					if (xmlNode2 != null && xmlNode2.InnerText.CompareTo(torrentSearch_Generic.Parameters["Tracker"]) == 0)
					{
						torrentSearch_Generic.Parameters["TrackerURL"] = xmlNode2.NextSibling.InnerText;
						XmlNode xmlNode12 = item5.SelectSingleNode("authentication");
						if (xmlNode12 != null)
						{
							torrentSearch_Generic.Parameters["Authentication.Method"] = xmlNode12.SelectSingleNode("method").InnerText;
							torrentSearch_Generic.Parameters["Authentication.LoginUrl"] = xmlNode12.SelectSingleNode("loginurl").InnerText;
							torrentSearch_Generic.Parameters["Authentication.Post"] = xmlNode12.SelectSingleNode("post").InnerText;
						}
						break;
					}
				}
				foreach (XmlNode item6 in item2.SelectNodes("sort"))
				{
					innerText = item6.SelectSingleNode("name").InnerText;
					string innerText3 = item6.SelectSingleNode("url").InnerText;
					torrentSearch_Generic.Parameters["Sort." + innerText] = innerText3;
				}
				_engines.Add(torrentSearch_Generic);
			}
			SetUpTVSeriesEngine();
		}

		/// Name of the engine used when MP-TVSeries (or another plugin) opens MyTorrents with a search.
		public const string TVSeriesEngineName = "mptvseries";

		/// 2026: makes sure the "mptvseries" engine exists and searches Jackett (all indexers) plus every
		/// enabled site you added yourself, so TV searches work with or without Jackett.
		private void SetUpTVSeriesEngine()
		{
			TorrentSearch_Combiner combiner = _engines.OfType<TorrentSearch_Combiner>().FirstOrDefault(c => c.Name == TVSeriesEngineName);
			if (combiner == null)
			{
				combiner = new TorrentSearch_Combiner(TVSeriesEngineName) { Enabled = "true" };
				combiner.AddSort("Default", "Default", "Seeds"); // engines asked for "Default", results sorted by seeds
				_engines.Add(combiner);
			}
			ITorrentSearch jackettAll = _engines.FirstOrDefault(e => e.Name == JackettAggregateEngine.AllIndexersName);
			if (jackettAll != null && !combiner.Engines.Contains(jackettAll))
			{
				combiner.Engines.Add(jackettAll);
			}
			foreach (SimpleSiteSearchProvider site in _engines.OfType<SimpleSiteSearchProvider>())
			{
				if (!string.Equals(site.Enabled, "false", StringComparison.OrdinalIgnoreCase) && !combiner.Engines.Contains(site))
				{
					combiner.Engines.Add(site);
				}
			}
		}

		public ITorrentSearch Create(string engineId)
		{
			engineId = engineId.ToLower();
			foreach (ITorrentSearch engine in _engines)
			{
				if (engine.Name.ToLower().CompareTo(engineId) == 0)
				{
					return engine;
				}
			}
			return null;
		}

		public void SaveEnabledState(string config)
		{
			XmlDocument xmlDocument = new XmlDocument();
			xmlDocument.Load(config);
			XmlNodeList xmlNodeList = xmlDocument.SelectNodes("config/searchengines/engine");
			foreach (XmlNode engine in xmlNodeList)
			{
				XmlNode xmlNode = engine.SelectSingleNode("enabled");
				List<ITorrentSearch> engines = Engines;
				Func<ITorrentSearch, bool> predicate = (ITorrentSearch e) => e.Name == engine.SelectSingleNode("name").InnerText;
				ITorrentSearch match = engines.FirstOrDefault(predicate);
				if (match != null)
				{
					xmlNode.InnerText = match.Enabled;
				}
			}
		}
	}
}
