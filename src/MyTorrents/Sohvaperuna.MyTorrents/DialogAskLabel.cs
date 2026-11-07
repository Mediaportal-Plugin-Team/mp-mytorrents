using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;
using System.Linq;
using Sohvaperuna.TorrentEngine;

namespace Sohvaperuna.MyTorrents
{
	internal class DialogAskLabel
	{
		public static TorrentLabel Ask()
		{
			TorrentLabel torrentLabel = new TorrentLabel();
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return null;
			}
			// 2026: nothing to choose when there are no labels - do not ask
			var labels = TorrentEngine.Instance().TorrentSession.Labels;
			if (labels == null || !labels.Exists(l => !string.IsNullOrEmpty(l.Name) && l.Name != "No label"))
			{
				torrentLabel.Name = "";
				return torrentLabel;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading("Under which label?");
			foreach (TorrentLabel label in TorrentEngine.Instance().TorrentSession.Labels)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = ((label.Name == "") ? "No label" : label.Name);
				gUIListItem.AlbumInfoTag = label;
				gUIDialogMenu.Add(gUIListItem);
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			Menus.Check(gUIDialogMenu);
			if (gUIDialogMenu.SelectedLabel == -1)
			{
				return null;
			}
			// 2026: "No label" is the empty label, not a label called "No label"
			TorrentLabel chosen = gUIDialogMenu.SelectedLabel >= 0 && gUIDialogMenu.SelectedLabel < labels.Count ? labels[gUIDialogMenu.SelectedLabel] : null;
			torrentLabel.Name = chosen != null ? (chosen.Name == "No label" ? "" : chosen.Name) : (gUIDialogMenu.SelectedLabelText == "No label" ? "" : gUIDialogMenu.SelectedLabelText);
			return torrentLabel;
		}
	}
}
