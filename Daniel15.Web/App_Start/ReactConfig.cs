using React;

namespace Daniel15.Web
{
	public static class ReactConfig
	{
		public static void Configure()
		{
			ReactSiteConfiguration.Configuration = new ReactSiteConfiguration()
				.AddScript("~/Content/js/socialfeed.jsx")
				.SetUseHarmony(true);
		}
	}
}