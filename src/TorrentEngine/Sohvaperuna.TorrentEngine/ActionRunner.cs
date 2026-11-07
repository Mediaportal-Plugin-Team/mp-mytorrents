using System.Timers;

namespace Sohvaperuna.TorrentEngine
{
	public class ActionRunner
	{
		private long _after;

		private IAction _action;

		private bool _ownThread;

		private Timer _timer;

		public event ActionDone ActionDone;

		public ActionRunner(IAction action, long after, bool ownThread)
		{
			_action = action;
			_after = after;
			_ownThread = ownThread;
			_timer = new Timer(after);
			_timer.Elapsed += _timer_Elapsed;
			_timer.AutoReset = false;
			_timer.Enabled = true;
		}

		private void _timer_Elapsed(object sender, ElapsedEventArgs e)
		{
			_action.DoIt();
			if (this.ActionDone != null)
			{
				this.ActionDone(this);
			}
		}
	}
}
