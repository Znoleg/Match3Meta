using share.controller.game.events.local.ProductSales;
using share.controller.GUI.events;
using share.controller.statistic.eventDummies.core;
using share.model.events;
using share.model.events.local;
using share.model.user.events;
using share.model.user.events.core;
using share.model.user.events.local;
using share.model.user.events.local.core;
using UnityEngine;
using UnityEngine.UI;

namespace UI.Events.OneProduct {
	public abstract class ProductSaleIconPresenterBase<TSaleController, TSaleWindowController, TProductSalePresenter> : 
		ControllerEventIconPresenter<TSaleController, ProductSaleData, ProductSaleModel> 
		where TProductSalePresenter : ProductSalePresenterBase 
		where TSaleController : ProductSaleControllerBase 
		where TSaleWindowController : ProductSaleWindowBase<TSaleController, TProductSalePresenter> 
	{
		[SerializeField] private Button _button = default;

		protected abstract string IconClickStatisticSendName { get; }
		protected override bool GetControllerByProtoId => true;

		protected override void Init(IActiveLocalEvent data) {
			_button.onClick.RemoveAllListeners();
			_button.onClick.AddListener(OpenWindow);
			base.Init(data);
		}
		
		private void OpenWindow() {
			if (SystemController.ViewManager.CurrentSceneIsMeta) {
				GetController().SendStatistic(IconClickStatisticSendName, StatisticEventActionsAction.Open);
				SystemController.ViewManager.Show<TSaleWindowController, string>(GetController().GetMainWindow(), ActiveLocalEventData.GetProto().Id);	
				GetController().ClearActionsOnWindowShow(); // возможно лучше перенести на открытие окна
			}
		}
	}
}