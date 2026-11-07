namespace AzureusEngine.AzCommunication
{
	public enum DownloadState
	{
		ST_WAITING = 1,
		ST_PREPARING,
		ST_READY,
		ST_DOWNLOADING,
		ST_SEEDING,
		ST_STOPPING,
		ST_STOPPED,
		ST_ERROR,
		ST_QUEUED
	}
}
