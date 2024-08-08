using share.controller.game.events.core;
using share.controller.game.events.local.core;
using share.controller.game.events.ServerEvents;
using share.controller.game.HUDMagicTime;
using share.controller.GUI;
using share.model.events.local.core;
using share.model.user.events.local.core;
using UnityEngine;

namespace UI.Events {
	public abstract class ControllerEventIconPresenter<TController, TData, TModel> : EventIconPresenter
		where TController : BaseLocalEventController<TData, TModel> 
		where TData : ActiveLocalEventBase<TModel>
		where TModel : BaseLocalEventModel
	{
		private TController _controller;
		private TData _data;
		private TModel _model;

		protected virtual bool GetControllerByProtoId => false;
		
		protected override long GetTimeLeft() {
			return GetController()?.GetRemainingTime() ?? 0;
		}

		protected sealed override INotificatorStateProvider GetNotificatorStateProvider() {
			#if UNITY_EDITOR
			if (GetController() is not INotificatorStateProvider) {
				Debug.Log($"Контроллер для иконки {name} не реализовывает интерфейс {nameof(INotificatorStateProvider)}! Реализуйте / не используйте нотификатор");
			}
			#endif
			
			return GetController() as INotificatorStateProvider;
		}

		protected sealed override IRemainingTimeProvider GetRemainingServerTimeProvider() {
			return GetController();
		}

		protected sealed override bool NeedsHudProgressAnimation() {
			if (GetController() is IHudMTController hudMTController) {
				return hudMTController.MeetsHudMTConditions();
			}
			
			return base.NeedsHudProgressAnimation();
		}

		protected sealed override bool HasUnseenRewards() {
			if (GetController() is IHaveIconDisplayRewards haveIconDisplayRewards) {
				return haveIconDisplayRewards.HasUnseenRewards;
			}

			return base.HasUnseenRewards();
		}

		protected TController GetController() {
			return _controller ??= GetControllerByProtoId ? 
				SystemController.GameController.LocalEventsController.GetEventControllerById<TController>(ActiveLocalEventData.GetProto().Id) 
				: SystemController.GameController.LocalEventsController.GetEventController<TController>();
		}
		
		protected TData GetData() {
			return _data ??= GetController().GetData<TData>();
		}
		
		protected TModel GetProto() {
			return _model ??= GetController().GetProto<TModel>();
		}
	}
}