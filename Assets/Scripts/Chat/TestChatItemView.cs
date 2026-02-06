using InfiniteScroller;
using TMPro;
using UnityEngine;

namespace Chat
{
	public class TestChatItemView : InfiniteScrollerItemView
	{
		[SerializeField] private TextMeshProUGUI _message;

		protected override void DoInitialize(object data)
		{
			_message?.SetText(data?.ToString() ?? string.Empty);
		}
	}
}