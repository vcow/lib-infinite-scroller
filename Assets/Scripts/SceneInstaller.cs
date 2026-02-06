using Chat;
using InfiniteScroller;
using VContainer;
using VContainer.Unity;

public class SceneInstaller : LifetimeScope
{
	protected override void Configure(IContainerBuilder builder)
	{
		builder.Register<TestChatDataProvider>(Lifetime.Singleton).As<IInfiniteScrollerDataProvider>();
	}
}