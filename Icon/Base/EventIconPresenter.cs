using share.model.user.events.local.core;
using UnityEngine;

namespace UI.Events {
	public abstract class EventIconPresenter : BaseEventIconPresenter {
		protected IActiveLocalEvent ActiveLocalEventData { get; private set; }

		public EventIconPresenter Clone(Transform parent, IActiveLocalEvent data) {
			EventIconPresenter presenter = Instantiate(this, parent);
			presenter.Init(data);
			return presenter;
		}

		protected virtual void Init(IActiveLocalEvent data) {
			ActiveLocalEventData = data;
			InitInternal();
		}
	}
}