using System.Collections.Generic;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MediaPortalExt
{
	internal class Menu
	{
		private List<IMenuItem> _menuItems;

		internal List<IMenuItem> MenuItems => _menuItems;

		public Menu()
		{
			_menuItems = new List<IMenuItem>();
		}

		public IMenuItem Show(string heading)
		{
			GUIDialogMenu gUIDialogMenu = (GUIDialogMenu)GUIWindowManager.GetWindow(2012);
			if (gUIDialogMenu == null)
			{
				return null;
			}
			gUIDialogMenu.Reset();
			gUIDialogMenu.SetHeading(heading);
			foreach (IMenuItem menuItem in _menuItems)
			{
				GUIListItem gUIListItem = new GUIListItem();
				gUIListItem.Label = menuItem.Label;
				gUIListItem.AlbumInfoTag = menuItem;
				gUIDialogMenu.Add(gUIListItem);
			}
			gUIDialogMenu.DoModal(GUIWindowManager.ActiveWindow);
			if (gUIDialogMenu.SelectedLabel == -1)
			{
				return null;
			}
			return _menuItems[gUIDialogMenu.SelectedLabel];
		}
	}
}
