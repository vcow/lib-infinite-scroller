using InfiniteScroller;
using TMPro;
using UnityEngine;

namespace Chat
{
	public class TestChatItemView : InfiniteScrollerItemView<string>
	{
		[SerializeField] private TextMeshProUGUI _message;

		protected override void DoInitialize(string data)
		{
			gameObject.name = Key.ToString();
			_message?.SetText(data ?? string.Empty);
		}
	}
}