using System.Collections.Generic;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MyTorrents
{
	public class GUICheckListDialog : GUIDialogMenu
	{
		public const int ID = 2102;

		[SkinControl(3)]
		public GUICheckListControl SelectionList = null;

		[SkinControl(10)]
		protected GUIButtonControl btnOK = null;

		[SkinControl(11)]
		protected GUIButtonControl btnCancel = null;

		public ModalResult DialogModalResult = ModalResult.None;

		protected bool m_bRunning = true;

		public List<GUIListItem> ListItems = new List<GUIListItem>();

		public override int GetID => 2102;

		public GUICheckListDialog()
		{
			GetID = 2102;
		}

		public override bool Init()
		{
			SkinSetup.EnsureAllSkins();
			return Load(GUIGraphicsContext.Skin + "\\MP-MyTorrents.DialogCheckList.xml");
		}

		public override string GetModuleName()
		{
			return "MultiSelectDialog";
		}

		protected override void OnClicked(int controlId, GUIControl control, Action.ActionType actionType)
		{
			if (control == btnOK)
			{
				DialogModalResult = ModalResult.OK;
				Close();
			}
			if (control == btnCancel)
			{
				DialogModalResult = ModalResult.Cancel;
				Close();
			}
			if (control == SelectionList && actionType == Action.ActionType.ACTION_SELECT_ITEM && SelectionList.SelectedListItem != null)
			{
				SelectionList.SelectedListItem.Selected = !SelectionList.SelectedListItem.Selected;
			}
			else
			{
				base.OnClicked(controlId, control, actionType);
			}
		}

		public override void DoModal(int dwParentId)
		{
			m_bRunning = true;
			DialogModalResult = ModalResult.None;
			base.DoModal(dwParentId);
		}

		public new void Reset()
		{
			ListItems.Clear();
			base.Reset();
		}

		public new void Add(string strLabel)
		{
			int itemId = ListItems.Count + 1;
			GUIListItem gUIListItem = new GUIListItem();
			if (base.ShowQuickNumbers)
			{
				gUIListItem.Label = itemId + " " + strLabel;
			}
			else
			{
				gUIListItem.Label = strLabel;
			}
			gUIListItem.ItemId = itemId;
			ListItems.Add(gUIListItem);
			base.Add(strLabel);
		}

		public new void Add(GUIListItem pItem)
		{
			ListItems.Add(pItem);
			base.Add(pItem);
		}

		public virtual void Close()
		{
			if (m_bRunning)
			{
				m_bRunning = false;
				GUIWindowManager.IsSwitchingToNewWindow = true;
				lock (this)
				{
					GUIMessage message = new GUIMessage(GUIMessage.MessageType.GUI_MSG_WINDOW_DEINIT, GetID, 0, 0, 0, 0, null);
					OnMessage(message);
					GUIWindowManager.UnRoute();
					m_bRunning = false;
				}
				GUIWindowManager.IsSwitchingToNewWindow = false;
			}
		}
	}
}
