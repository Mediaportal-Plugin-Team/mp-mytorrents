namespace Sohvaperuna.MediaPortalExt
{
	internal interface IMenuItem
	{
		string Label { get; }

		void Act();
	}
}
