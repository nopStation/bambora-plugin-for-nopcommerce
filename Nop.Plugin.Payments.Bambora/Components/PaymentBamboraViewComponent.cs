using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.Bambora.Components
{
    [ViewComponent(Name = "PaymentBambora")]
    public class PaymentBamboraViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.Bambora/Views/PaymentInfo.cshtml");
        }
    }
}
