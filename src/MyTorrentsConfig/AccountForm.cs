using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrentsConfig
{
	/// <summary>
	/// 2026: "Log in to [site]" for sites that need an account. Shows the boxes and help text Jackett has for that site
	/// (username/password, cookie, API key...), then adds the site - Jackett logs in to test it. The login is kept by
	/// Jackett on this PC; MyTorrents does not store it.
	/// </summary>
	internal sealed class AccountForm : Form
	{
		private readonly JackettApi _api;
		private readonly JackettApi.Site _site;
		private readonly object[] _config;
		private readonly Dictionary<Dictionary<string, object>, TextBox> _boxes = new Dictionary<Dictionary<string, object>, TextBox>();
		private readonly Label _error = new Label { ForeColor = Color.FromArgb(196, 43, 28), AutoSize = false };
		private readonly Button _ok = new Button { Text = "Log in and add" };
		private readonly Button _cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };

		public string JackettUrl = "http://127.0.0.1:9117";

		/// Null when the site needs something this window cannot show (a picture puzzle) - use Jackett's page.
		public static string CannotHandle(object[] config)
		{
			foreach (Dictionary<string, object> f in config.OfType<Dictionary<string, object>>())
			{
				string type = Str(f, "type").ToLowerInvariant();
				if (type.Contains("captcha")) return "it asks for a picture puzzle (captcha) when logging in";
			}
			return null;
		}

		public AccountForm(JackettApi api, JackettApi.Site site, object[] config)
		{
			_api = api;
			_site = site;
			_config = config;
			Text = "Log in to " + site.Name;
			Font = new Font("Segoe UI", 9f);
			FormBorderStyle = FormBorderStyle.FixedDialog;
			MaximizeBox = MinimizeBox = false;
			StartPosition = FormStartPosition.CenterParent;
			ClientSize = new Size(520, 480);

			Controls.Add(new Label
			{
				Text = site.Name + " needs your account on that site. Fill in the boxes below - they are what Jackett asks for this site.\r\n" +
					"Your login is kept by Jackett on this PC. MyTorrents does not store it.",
				Location = new Point(14, 10), Size = new Size(492, 52)
			});
			FlowLayoutPanel panel = new FlowLayoutPanel { Location = new Point(14, 66), Size = new Size(492, 318), FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, BorderStyle = BorderStyle.FixedSingle, Padding = new Padding(6) };
			Controls.Add(panel);
			foreach (Dictionary<string, object> f in config.OfType<Dictionary<string, object>>())
			{
				string id = Str(f, "id"), type = Str(f, "type"), name = Str(f, "name"), value = Str(f, "value");
				if (type == "displayinfo")
				{
					string text = Plain(value);
					if (text.Length == 0 || id == "flaresolverr" || id == "resultsperpage") continue;
					panel.Controls.Add(new Label { Text = (name.Length > 0 ? name + ": " : "") + text, MaximumSize = new Size(460, 0), AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 2, 0, 8) });
				}
				else if (type == "inputstring" && id != "sitelink")
				{
					panel.Controls.Add(new Label { Text = name, AutoSize = true, Margin = new Padding(0, 4, 0, 0) });
					bool secret = Regex.IsMatch(id + " " + name, "pass|secret|token|apikey|api key|pin", RegexOptions.IgnoreCase) && !Regex.IsMatch(name, "user", RegexOptions.IgnoreCase);
					TextBox box = new TextBox { Width = 455, Text = value, UseSystemPasswordChar = secret };
					panel.Controls.Add(box);
					_boxes[f] = box;
				}
			}
			panel.Controls.Add(new Label { Text = "Other options for this site (freeleech only, sorting...) can be changed later on Jackett's own page.", MaximumSize = new Size(460, 0), AutoSize = true, ForeColor = Color.DimGray, Margin = new Padding(0, 10, 0, 0) });

			_error.SetBounds(14, 390, 492, 40);
			Controls.Add(_error);
			LinkLabel jackett = new LinkLabel { Text = "Use Jackett's own page instead", AutoSize = true, Location = new Point(14, 446) };
			jackett.LinkClicked += (s, e) => SetupForm.OpenUrl(JackettUrl);
			Controls.Add(jackett);
			_ok.SetBounds(296, 440, 120, 30);
			_cancel.SetBounds(422, 440, 84, 30);
			Controls.Add(_ok);
			Controls.Add(_cancel);
			AcceptButton = _ok;
			CancelButton = _cancel;
			_ok.Click += (s, e) => Save();
		}

		private void Save()
		{
			foreach (var kv in _boxes)
			{
				kv.Key["value"] = kv.Value.Text.Trim();
			}
			if (_boxes.Count > 0 && _boxes.Values.All(b => b.Text.Trim().Length == 0))
			{
				_error.Text = "Fill in the boxes first.";
				return;
			}
			_ok.Enabled = _cancel.Enabled = false;
			_error.ForeColor = Color.DimGray;
			_error.Text = "Logging in to " + _site.Name + " - this can take up to a minute...";
			Cursor = Cursors.WaitCursor;
			ThreadPool.QueueUserWorkItem(_ =>
			{
				string problem = null;
				try
				{
					_api.SaveConfig(_site.Id, _config);
					SetupCore.Log("Jackett: added " + _site.Name + " (account)");
				}
				catch (Exception ex)
				{
					problem = ex.Message;
					SetupCore.Log("Jackett: could not add " + _site.Name + ": " + ex.Message);
				}
				if (IsDisposed) return;
				BeginInvoke((Action)(() =>
				{
					Cursor = Cursors.Default;
					_ok.Enabled = _cancel.Enabled = true;
					if (problem == null)
					{
						DialogResult = DialogResult.OK;
						Close();
						return;
					}
					_error.ForeColor = Color.FromArgb(196, 43, 28);
					_error.Text = "The site did not accept that: " + problem;
				}));
			});
		}

		private static string Str(Dictionary<string, object> d, string key) => d.ContainsKey(key) && d[key] != null ? Convert.ToString(d[key]) : "";

		/// Jackett's help text is HTML - turn it into plain lines.
		private static string Plain(string html)
		{
			string s = Regex.Replace(html ?? "", @"<br\s*/?>|</li>|</p>", "\n", RegexOptions.IgnoreCase);
			s = Regex.Replace(s, @"<li[^>]*>", "  - ", RegexOptions.IgnoreCase);
			s = Regex.Replace(s, "<[^>]+>", "");
			s = WebUtility.HtmlDecode(s);
			s = Regex.Replace(s, @"[ \t]+", " ");
			s = Regex.Replace(s, @"\n\s*\n+", "\n").Trim();
			return s;
		}
	}
}
