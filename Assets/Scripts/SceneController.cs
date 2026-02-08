using Chat;
using Plugins.vcow.InfiniteScroller;
using UnityEngine;
using VContainer;

[DisallowMultipleComponent]
public class SceneController : MonoBehaviour
{
	[Inject] private readonly IInfiniteScrollerDataProvider<string> _scrollerDataProvider;

	public void AddItem()
	{
		((TestChatDataProvider)_scrollerDataProvider).ADD(null);
	}
}