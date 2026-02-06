using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using R3;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.Pool;
using UnityEngine.UI;
using VContainer;

namespace InfiniteScroller
{
	[DisallowMultipleComponent, RequireComponent(typeof(RectTransform), typeof(RawImage))]
	public sealed class InfiniteVScroller : MonoBehaviour, IInitializePotentialDragHandler,
		IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		[SerializeField] private InfiniteScrollerItemView _itemViewPrefab;
		[SerializeField, Space] private RectTransform _contentContainer;
		[SerializeField] private bool _inertiaEnabled = true;
		[SerializeField, Range(0.001f, 1f)] private float _decelerationRate = 0.135f;

		[Inject] private readonly IInfiniteScrollerDataProvider _dataProvider;

		private readonly CompositeDisposable _disposables = new();
		private readonly CancellationTokenSource _cts = new();

		private RectTransform _rectTransform;
		private ObjectPool<InfiniteScrollerItemView> _itemViewPool;
		private bool _isDragging;

		private Vector2 _pointerPosition;
		private float _velocity;

		private void OnValidate()
		{
			Assert.IsNotNull(_contentContainer, "ContentContainer must not be null!");
			Assert.IsNotNull(_itemViewPrefab, "ItemViewPrefab must not be null!");
		}

		private void Awake()
		{
			_rectTransform = (RectTransform)transform;

			_disposables.Add(_itemViewPool);
			_itemViewPool = new ObjectPool<InfiniteScrollerItemView>(
				() =>
				{
					var item = Instantiate(_itemViewPrefab, _contentContainer);
					AlignTransform(item.GetComponent<RectTransform>());
					return item;
				},
				view => view.gameObject.SetActive(true),
				view => view.gameObject.SetActive(false),
				view => Destroy(view.gameObject));
		}

		private void Start()
		{
			Assert.IsNotNull(_contentContainer);
			AlignTransform(_contentContainer);

			ApplyContent();

			_dataProvider.AddObservable
				.Subscribe(itemData => { AddItemInBottom(itemData.key, itemData.data); })
				.AddTo(_disposables);
			_dataProvider.RemoveObservable
				.Subscribe(_ => throw new NotImplementedException())
				.AddTo(_disposables);
		}

		private void OnDestroy()
		{
			_cts.Cancel();
			_disposables.Dispose();
		}

		private void ApplyContent()
		{
			Assert.IsNotNull(_contentContainer);
			foreach (Transform child in _contentContainer)
			{
				var itemView = child.GetComponent<InfiniteScrollerItemView>();
				if (itemView != null)
				{
					_itemViewPool.Release(itemView);
				}
			}

			LayoutRebuilder.ForceRebuildLayoutImmediate(_rectTransform);

			var scrollRectBounds = new Bounds(_rectTransform.rect.center, _rectTransform.rect.size);
			var contentContainerHeight = 0f;

			foreach (var (key, data) in _dataProvider.Items.Reverse())
			{
				var itemView = _itemViewPool.Get();
				itemView.Initialize(key, data);
				var itemRectTransform = itemView.rectTransform;
				LayoutRebuilder.ForceRebuildLayoutImmediate(itemRectTransform);
				itemRectTransform.anchoredPosition = new Vector2(0, contentContainerHeight);
				contentContainerHeight += itemRectTransform.rect.height;
				if (contentContainerHeight >= scrollRectBounds.size.y)
				{
					break;
				}
			}

			_contentContainer.sizeDelta = new Vector2(_contentContainer.sizeDelta.x, contentContainerHeight);
		}

		private void AddItemOnTop(int key, object itemData)
		{
		}

		private void AddItemInBottom(int key, object itemData)
		{
		}

		private static void AlignTransform(RectTransform rectTransform)
		{
			rectTransform.anchorMin = new Vector2(0f, 0f);
			rectTransform.anchorMax = new Vector2(1f, 0f);
			rectTransform.pivot = new Vector2(0.5f, 0f);
			rectTransform.offsetMin = new Vector2(0f, rectTransform.offsetMin.y);
			rectTransform.offsetMax = new Vector2(0f, rectTransform.offsetMax.y);
			rectTransform.anchoredPosition = new Vector2(0f, 0f);
			rectTransform.sizeDelta = new Vector2(rectTransform.sizeDelta.x, 100f);
			rectTransform.localRotation = Quaternion.identity;
			rectTransform.localScale = Vector3.one;
		}

		void IInitializePotentialDragHandler.OnInitializePotentialDrag(PointerEventData eventData)
		{
			_pointerPosition = eventData.position;
			_isDragging = false;
			_velocity = 0f;
		}

		void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
		{
			_isDragging = _rectTransform.rect.height < _contentContainer.rect.height;
			if (_isDragging)
			{
				_velocity = 0f;
			}
		}

		void IDragHandler.OnDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			var offset = (eventData.position - _pointerPosition).y;
			_pointerPosition = eventData.position;

			if (!UpdateVisibleContentAndCorrectOffset(ref offset))
			{
				_contentContainer.anchoredPosition = Vector2.zero;
				_velocity = 0f;
				return;
			}

			_contentContainer.position += new Vector3(0f, offset, 0f);

			var unscaledDeltaTime = Time.unscaledDeltaTime;
			if (unscaledDeltaTime > 0f)
			{
				_velocity = offset / unscaledDeltaTime;
			}
			else
			{
				_velocity = 0f;
			}
		}

		void IEndDragHandler.OnEndDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			_isDragging = false;
			if (!_inertiaEnabled)
			{
				_velocity = 0f;
			}
		}

		private void Update()
		{
			if (_isDragging || !_inertiaEnabled || Mathf.Approximately(_velocity, 0f))
			{
				return;
			}

			var deltaTime = Time.unscaledDeltaTime;
			if (deltaTime <= 0f)
			{
				return;
			}

			var offset = _velocity * deltaTime;
			if (!UpdateVisibleContentAndCorrectOffset(ref offset))
			{
				_contentContainer.anchoredPosition = Vector2.zero;
				_velocity = 0f;
				return;
			}

			_contentContainer.position += new Vector3(0f, offset, 0f);

			if (Mathf.Approximately(offset, 0f))
			{
				_velocity = 0f;
				return;
			}

			_velocity *= Mathf.Pow(_decelerationRate, deltaTime);
			if (Mathf.Abs(_velocity) < 1f)
			{
				_velocity = 0f;
			}
		}

		private readonly List<InfiniteScrollerItemView> itemsToRelease = new();

		private bool UpdateVisibleContentAndCorrectOffset(ref float offset)
		{
			itemsToRelease.Clear();

			var visibleContentMinY = float.MaxValue;
			var visibleContentMaxY = float.MinValue;

			InfiniteScrollerItemView topItem = null;
			InfiniteScrollerItemView bottomItem = null;

			var scrollRectBounds = new Bounds(_rectTransform.rect.center, _rectTransform.rect.size);
			foreach (Transform child in _contentContainer)
			{
				var itemView = child.GetComponent<InfiniteScrollerItemView>();
				if (itemView == null || !itemView.gameObject.activeSelf)
				{
					continue;
				}

				var itemViewBounds = RectTransformUtility.CalculateRelativeRectTransformBounds(_rectTransform, itemView.rectTransform);
				if (itemViewBounds.max.y + offset < scrollRectBounds.min.y ||
				    itemViewBounds.min.y + offset > scrollRectBounds.max.y)
				{
					// out of view
					itemsToRelease.Add(itemView);
					continue;
				}

				if (topItem == null || topItem.rectTransform.anchoredPosition.y < itemView.rectTransform.anchoredPosition.y)
				{
					topItem = itemView;
					visibleContentMinY = Mathf.Min(visibleContentMinY, itemViewBounds.min.y);
					visibleContentMaxY = Mathf.Max(visibleContentMaxY, itemViewBounds.max.y);
				}

				if (bottomItem == null || bottomItem.rectTransform.anchoredPosition.y > itemView.rectTransform.anchoredPosition.y)
				{
					bottomItem = itemView;
					visibleContentMinY = Mathf.Min(visibleContentMinY, itemViewBounds.min.y);
					visibleContentMaxY = Mathf.Max(visibleContentMaxY, itemViewBounds.max.y);
				}
			}

			if (visibleContentMinY > visibleContentMaxY)
			{
				// list is empty
				offset = 0f;
				return false;
			}

			if (scrollRectBounds.min.y < visibleContentMinY + offset)
			{
				// leaving the lower limit
				offset = scrollRectBounds.min.y - visibleContentMinY;
			}
			else if (scrollRectBounds.max.y > visibleContentMaxY + offset)
			{
				// leaving the upper limit
				offset = scrollRectBounds.max.y - visibleContentMaxY;
			}

			return true;
		}
	}
}