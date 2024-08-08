using share.model.events.local;
using share.model.user.events.local;

namespace share.controller.game.events.local.ProductSales {
	public class GeneratedSaleController : ProductSaleControllerBase {
		public GeneratedSaleController(ProductSaleModel model) : base(model) {
		}

		public override string StatisticsSendName { get; }
		protected override Priority WindowPriority { get; }

		protected override ProductSaleData NewDataCreator(ProductSaleModel model) => new GeneratedProductSaleData(model as GeneratedProductSaleModel);
	}
}