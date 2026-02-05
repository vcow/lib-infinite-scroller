using System;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace InfiniteScroller
{
	[DisallowMultipleComponent, RequireComponent(typeof(RectTransform), typeof(RawImage))]
	public sealed class InfiniteVScroller : MonoBehaviour, IInitializePotentialDragHandler,
		IBeginDragHandler, IDragHandler, IEndDragHandler
	{
		[SerializeField] private RectTransform _contentContainer;

		private readonly Lazy<RectTransform> _rectTransform;

		private bool _isDragging;

		private Vector2 _startDragPosition;
		private Vector2 _startDragPointerPosition;

		public InfiniteVScroller()
		{
			_rectTransform = new Lazy<RectTransform>(GetComponent<RectTransform>);
		}

		private void OnValidate()
		{
			Assert.IsNotNull(_contentContainer, "ContentContainer must not be null!");
		}

		private void Start()
		{
			AlignContainer();
		}

		private void AlignContainer()
		{
			Assert.IsNotNull(_contentContainer);

			_contentContainer.anchorMin = new Vector2(0f, 0f);
			_contentContainer.anchorMax = new Vector2(1f, 0f);
			_contentContainer.pivot = new Vector2(0.5f, 0f);
			_contentContainer.offsetMin = new Vector2(0f, _contentContainer.offsetMin.y);
			_contentContainer.offsetMax = new Vector2(0f, _contentContainer.offsetMax.y);
			_contentContainer.anchoredPosition = new Vector2(0f, 0f);
			_contentContainer.sizeDelta = new Vector2(_contentContainer.sizeDelta.x, 100f);
			_contentContainer.localRotation = Quaternion.identity;
			_contentContainer.localScale = Vector3.one;
		}

		void IInitializePotentialDragHandler.OnInitializePotentialDrag(PointerEventData eventData)
		{
			_startDragPosition = _contentContainer.position;
			_startDragPointerPosition = eventData.position;
			_isDragging = false;
		}

		void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
		{
			_isDragging = _rectTransform.Value.rect.height < _contentContainer.rect.height;
		}

		void IDragHandler.OnDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			var delta = new Vector2(0, eventData.position.y - _startDragPointerPosition.y);
			_contentContainer.position = _startDragPosition + delta;
		}

		void IEndDragHandler.OnEndDrag(PointerEventData eventData)
		{
			if (!_isDragging)
			{
				return;
			}

			_isDragging = false;
		}
	}
}