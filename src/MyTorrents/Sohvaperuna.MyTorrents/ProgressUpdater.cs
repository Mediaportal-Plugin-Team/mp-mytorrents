using System.Timers;
using MediaPortal.Dialogs;
using MediaPortal.GUI.Library;

namespace Sohvaperuna.MyTorrents
{
	internal class ProgressUpdater
	{
		private Timer _progressTimer;

		private double _steps;

		public void SetAutoUpdate(double interval, double steps)
		{
			_progressTimer = new Timer();
			_progressTimer.Elapsed += _progressTimer_Elapsed;
			_progressTimer.Interval = interval;
			_progressTimer.Enabled = true;
			_steps = steps;
		}

		private void _progressTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			GUIDialogProgress gUIDialogProgress = (GUIDialogProgress)GUIWindowManager.GetWindow(101);
			int percentage = gUIDialogProgress.Percentage;
			percentage += (int)(100.0 / _steps);
			gUIDialogProgress.SetPercentage((percentage > 100) ? 100 : percentage);
			gUIDialogProgress.ShowProgressBar(DisplayBar: true);
			gUIDialogProgress.Progress();
		}
	}
}
