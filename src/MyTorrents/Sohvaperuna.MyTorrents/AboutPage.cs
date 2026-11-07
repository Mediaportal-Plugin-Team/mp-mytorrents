using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace Sohvaperuna.MyTorrents
{
	/// <summary>2026: "About" tab - name, version, credits and links.</summary>
	public class AboutPage : TabPage
	{
		/// The project's own page - change this when the 2026 version gets a page of its own (e.g. on GitHub).
		public const string ProjectPage = "https://www.team-mediaportal.com/extensions/other/mytorrents";

		private static readonly string[,] Links =
		{
			{ "Project page", ProjectPage },
			{ "Jackett", "https://github.com/Jackett/Jackett" },
			{ "FlareSolverr", "https://github.com/FlareSolverr/FlareSolverr" },
			{ "BiglyBT", "https://www.biglybt.com/" },
			{ "qBittorrent", "https://www.qbittorrent.org/" },
			{ "MediaPortal forum", "https://forum.team-mediaportal.com/" },
		};

		public AboutPage()
		{
			Text = "About";
			Padding = new Padding(3);
			UseVisualStyleBackColor = true;
			Font = new Font("Microsoft Sans Serif", 8.25f);

			int y = 12;
			Controls.Add(new Label { Text = "MyTorrents", Font = new Font("Microsoft Sans Serif", 14f, FontStyle.Bold), AutoSize = true, Location = new Point(12, y) });
			y += 30;
			Controls.Add(new Label { Text = "Version " + Version(), AutoSize = true, ForeColor = Color.DimGray, Location = new Point(14, y) });
			y += 26;
			Controls.Add(new Label
			{
				Text = "A MediaPortal plugin for finding and downloading TV episodes and movies with torrents. " +
					"It searches your sites through Jackett, sends downloads to qBittorrent or BiglyBT, copies finished " +
					"episodes and movies into your library, and its Auto grabber gets new episodes of your shows by itself.",
				Location = new Point(14, y), Size = new Size(530, 44)
			});
			y += 54;

			Controls.Add(Heading("Credits", y));
			y += 20;
			Controls.Add(new Label
			{
				Text = "Original MP-MyTorrents plugin by Sohvaperuna, later developed by Edalex (edalex86).\r\n" +
					"2026 update by Mark Willmer.  Thanks to everyone who worked on the original.",
				Location = new Point(14, y), Size = new Size(530, 32)
			});
			y += 42;

			Controls.Add(Heading("Links", y));
			y += 20;
			for (int i = 0; i < Links.GetLength(0); i++)
			{
				string url = Links[i, 1];
				Controls.Add(new Label { Text = Links[i, 0], AutoSize = true, Location = new Point(14, y) });
				LinkLabel link = new LinkLabel { Text = url, AutoSize = true, Location = new Point(140, y) };
				link.LinkClicked += (s, e) => Open(url);
				Controls.Add(link);
				y += 20;
			}
		}

		private static Label Heading(string text, int y) =>
			new Label { Text = text, Font = new Font("Microsoft Sans Serif", 8.25f, FontStyle.Bold), AutoSize = true, Location = new Point(12, y) };

		/// MyTorrents.dll's file version, e.g. "1.3.36"
		public static string Version()
		{
			try
			{
				string v = FileVersionInfo.GetVersionInfo(typeof(AboutPage).Assembly.Location).FileVersion ?? "";
				return v.EndsWith(".0") ? v.Substring(0, v.Length - 2) : v;
			}
			catch
			{
				return "?";
			}
		}

		private void Open(string url)
		{
			try
			{
				Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				MessageBox.Show(this, "Could not open " + url + "\r\n\r\n" + ex.Message, "MyTorrents");
			}
		}
	}
}
