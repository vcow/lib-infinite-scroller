using System;
using Plugins.vcow.InfiniteScroller;
using Random = UnityEngine.Random;

namespace Chat
{
	public class TestChatDataProvider : ChatDataProviderBase<string>, IDisposable
	{
		private const string phrase = "И он бросил на палубу целую пригоршню замерзших слов, похожих на драже, переливающихся разными цветами. Здесь были красные, зеленые, лазуревые и золотые. В наших руках они согревались и таяли, как снег, и тогда мы их действительно слышали, но не понимали, так как это был какой-то варварский язык...";
		private const int numMessages = 100;

		public TestChatDataProvider()
		{
			var words = phrase.Split(' ');
			for (var i = 0; i < numMessages; ++i)
			{
				ADD(words);
			}
		}

		public void ADD(string[] words)
		{
			words ??= phrase.Split(' ');
			var sub = words.AsSpan(0, Random.Range(0, words.Length));
			var data = string.Join(" ", sub.ToArray());
			Add(data);
		}
	}
}