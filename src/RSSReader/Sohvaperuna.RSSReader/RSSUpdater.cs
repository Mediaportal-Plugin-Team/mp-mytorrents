using System.Timers;

namespace Sohvaperuna.RSSReader
{
	public class RSSUpdater
	{
		private Timer _updateTimer;

		public RSSUpdater()
		{
			_updateTimer = new Timer();
			_updateTimer.Elapsed += _updateTimer_Elapsed;
		}

		private void _updateTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			RSSChannelManager.Instance().UpdateChannels(force: false);
		}

		public void Start(long interval)
		{
			_updateTimer.Interval = interval;
			_updateTimer.Enabled = true;
			_updateTimer_Elapsed(this, null);
		}

		public void Stop()
		{
			_updateTimer.Enabled = false;
		}
	}
}
