using System;
using System.Reflection;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MyTorrents
{
	/// 2026 (1.3.57): every MyTorrents menu and pop-up goes through Check() straight after it closes.
	/// When MediaPortal is shutting down, or menus are closing again faster than anyone could press a button
	/// (a menu that reopens itself after each choice spun 24,000 times in 2 1/2 minutes when MediaPortal was closed
	/// with the Auto grabber menu open - laptop, 7 Nov), the pop-up is made to look as if nothing was chosen,
	/// so the code that asked ends quietly instead of opening it again.
	internal static class Menus
	{
		private static DateTime _last = DateTime.MinValue;
		private static int _quick;

		public static bool Closing
		{
			get
			{
				try
				{
					return GUIGraphicsContext.CurrentState == GUIGraphicsContext.State.STOPPING || GUIGraphicsContext.CurrentState == GUIGraphicsContext.State.SUSPENDING;
				}
				catch
				{
					return false;
				}
			}
		}

		public static void Check(object dialog)
		{
			DateTime now = DateTime.Now;
			_quick = now - _last < TimeSpan.FromMilliseconds(150) ? _quick + 1 : 0;
			_last = now;
			if (!Closing && _quick < 10) return;
			if (_quick == 10) Log.Instance().Print("Menus kept closing straight away (MediaPortal closing?) - stopped");
			Nothing(dialog);
		}

		private static void Nothing(object dialog)
		{
			if (dialog == null) return;
			Type t = dialog.GetType();
			foreach (string name in new[] { "SelectedLabel", "SelectedId" }) Set(dialog, t, name, -1);
			Set(dialog, t, "IsConfirmed", false);
			try
			{
				MethodInfo reset = t.GetMethod("Reset", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
				reset?.Invoke(dialog, null);
			}
			catch
			{
			}
		}

		private static void Set(object o, Type t, string name, object value)
		{
			try
			{
				PropertyInfo p = t.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
				if (p != null && p.CanWrite && p.PropertyType == value.GetType()) { p.SetValue(o, value, null); return; }
				for (Type x = t; x != null; x = x.BaseType)
				{
					foreach (FieldInfo f in x.GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
						if (f.FieldType == value.GetType() && string.Equals(f.Name.TrimStart('_', 'm'), name, StringComparison.OrdinalIgnoreCase)) { f.SetValue(o, value); return; }
				}
			}
			catch
			{
			}
		}
	}
}
