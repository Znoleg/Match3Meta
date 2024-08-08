using share.controller.game.events.local.ProductSales;
using share.controller.GUI.events;
using share.controller.GUI.events.TwoProduct;
using share.controller.statistic.eventDummies.core;
using UI.Events.OneProduct;

namespace UI.Events.TwoProduct
{
    public class TwoProductSaleIconPresenter : ProductSaleIconPresenterBase<TwoProductSaleController, TwoProductSaleWindowController, ProductSalePresenterBase> {
        protected override string IconClickStatisticSendName => StatisticEventActionsName.TwoProductSaleIcon;
    }
}