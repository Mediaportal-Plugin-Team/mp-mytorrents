using System.Collections.Generic;

namespace Sohvaperuna.TorrentEngine
{
	public class ActionController
	{
		private List<ActionRunner> _actions;

		public ActionController()
		{
			_actions = new List<ActionRunner>();
		}

		public void Invoke(IAction action, long after, bool ownThread)
		{
			ActionRunner actionRunner = new ActionRunner(action, after, ownThread);
			actionRunner.ActionDone += ar_ActionDone;
			_actions.Add(actionRunner);
		}

		private void ar_ActionDone(ActionRunner action)
		{
			_actions.Remove(action);
		}
	}
}
