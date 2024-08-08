using share.controller.GUI.events.CollectionsSale;
using share.controller.GUI.events.TwoProduct;
using share.controller.statistic.eventDummies.core;
using share.model.events.local;

namespace share.controller.game.events.local.ProductSales
{
    public class TwoProductSaleController : GeneratedSaleController
    {
        public TwoProductSaleController(ProductSaleModel model) : base(model) { }

        public override string StatisticsSendName => StatisticEventActionsName.TwoProductSaleWindow;
        protected override Priority WindowPriority => Priority.TwoProductSaleWindow;

        public override void TryShowSaleWindow(object _ = null) {
            if (!Data.isInit) return;
            base.TryShowSaleWindow(_);
        }

        protected override void ShowSaleWindow() {
            if (Conditions.RepeatableLevelLogic != null) {
                SystemController.ViewManager.Show<CollectionsSaleWindowController, string>(Proto.MainWindow, Proto.Id);
            } else {
                SystemController.ViewManager.Show<TwoProductSaleWindowController, string>(Proto.MainWindow, Proto.Id);
            }
        }
    }
}