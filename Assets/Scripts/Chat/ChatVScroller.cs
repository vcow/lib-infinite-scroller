using Plugins.vcow.InfiniteScroller;
using UnityEngine;
using UnityEngine.UI;

namespace Chat
{
	[DisallowMultipleComponent, RequireComponent(typeof(RectTransform), typeof(RawImage))]
	public sealed class ChatVScroller : InfiniteVScrollerBase<string>
	{
	}
}