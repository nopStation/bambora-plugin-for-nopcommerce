using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Plugin.Payments.Bambora.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Security;
using Nop.Services.Stores;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.Bambora.Controllers
{
    public class PaymentBamboraController : BasePaymentController
    {
        #region Fields

        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly INotificationService _notificationService;
        private readonly IPermissionService _permissionService;

        #endregion

        #region Ctor

        public PaymentBamboraController(ILocalizationService localizationService,
            ILogger logger,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            ISettingService settingService,
            IStoreContext storeContext,
            INotificationService notificationService,
            IPermissionService permissionService)
        {
            _localizationService = localizationService;
            _logger = logger;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _settingService = settingService;
            _storeContext = storeContext;
            _notificationService = notificationService;
            _permissionService = permissionService;
        }

        #endregion

        #region Utilities

        private IDictionary<string, string> GetParameters(IFormCollection form)
        {
            var requestParams = form.ToDictionary(pair => pair.Key, pair => pair.Value.ToString());
            foreach (var keyValuePair in Request.Query.Where(pair => !requestParams.ContainsKey(pair.Key)))
            {
                requestParams.Add(keyValuePair.Key, keyValuePair.Value);
            }

            IDictionary<string, string> parameters = new Dictionary<string, string>
            {
                { "trnApproved", requestParams["trnApproved"] },
                { "trnId", requestParams["trnId"] },
                { "messageId", requestParams["messageId"] },
                { "messageText", requestParams["messageText"] },
                { "authCode", requestParams["authCode"] },
                { "responseType", requestParams["responseType"] },
                { "trnAmount", requestParams["trnAmount"] },
                { "trnDate", requestParams["trnDate"] },
                { "trnOrderNumber", requestParams["trnOrderNumber"] },
                { "trnLanguage", requestParams["trnLanguage"] },
                { "trnCustomerName", requestParams["trnCustomerName"] },
                { "trnEmailAddress", requestParams["trnEmailAddress"] },
                { "trnPhoneNumber", requestParams["trnPhoneNumber"] },
                { "avsProcessed", requestParams["avsProcessed"] },
                { "avsId", requestParams["avsId"] },
                { "avsResult", requestParams["avsResult"] },
                { "avsPostalMatch", requestParams["avsPostalMatch"] },
                { "avsMessage", requestParams["avsMessage"] },
                { "cvdId", requestParams["cvdId"] },
                { "cardType", requestParams["cardType"] },
                { "trnType", requestParams["trnType"] },
                { "paymentMethod", requestParams["paymentMethod"] }
            };

            return parameters;
        }

        #endregion

        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var bamboraPaymentSettings = await _settingService.LoadSettingAsync<BamboraPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                MerchantId = bamboraPaymentSettings.MerchantId,
                HashKey = bamboraPaymentSettings.HashKey,
                AdditionalFee = bamboraPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = bamboraPaymentSettings.AdditionalFeePercentage,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.MerchantId_OverrideForStore = await _settingService.SettingExistsAsync(bamboraPaymentSettings, x => x.MerchantId, storeScope);
                model.HashKey_OverrideForStore = await _settingService.SettingExistsAsync(bamboraPaymentSettings, x => x.HashKey, storeScope);
                model.AdditionalFee_OverrideForStore = await _settingService.SettingExistsAsync(bamboraPaymentSettings, x => x.AdditionalFee, storeScope);
                model.AdditionalFeePercentage_OverrideForStore = await _settingService.SettingExistsAsync(bamboraPaymentSettings, x => x.AdditionalFeePercentage, storeScope);
            }

            return View("~/Plugins/Payments.Bambora/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = await _storeContext.GetActiveStoreScopeConfigurationAsync();
            var bamboraPaymentSettings = await _settingService.LoadSettingAsync<BamboraPaymentSettings>(storeScope);

            //save settings
            bamboraPaymentSettings.MerchantId = model.MerchantId;
            bamboraPaymentSettings.HashKey = model.HashKey;
            bamboraPaymentSettings.AdditionalFee = model.AdditionalFee;
            bamboraPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            await _settingService.SaveSettingOverridablePerStoreAsync(bamboraPaymentSettings, x => x.MerchantId, model.MerchantId_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(bamboraPaymentSettings, x => x.HashKey, model.HashKey_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(bamboraPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            await _settingService.SaveSettingOverridablePerStoreAsync(bamboraPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

            //now clear settings cache
            await _settingService.ClearCacheAsync();

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return RedirectToAction("Configure");
        }

        public async Task<IActionResult> ResultHandler(IFormCollection form)
        {
            var parameters = GetParameters(form);
            int orderId;
            if (!int.TryParse(parameters["trnOrderNumber"], out orderId))
                return RedirectToRoute("HomePage");

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order == null)
                return RedirectToRoute("HomePage");

            var sb = new StringBuilder();
            sb.AppendLine("Bambora payment result:");
            foreach (var parameter in parameters)
            {
                sb.AppendFormat("{0}: {1}{2}", parameter.Key, parameter.Value, Environment.NewLine);
            }

            //order note
            await _orderService.InsertOrderNoteAsync(new OrderNote()
            {
                Note = sb.ToString(),
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        public async Task<IActionResult> ResponseNotificationHandler(IFormCollection form)
        {
            var parameters = GetParameters(form);
            int orderId;
            if (!int.TryParse(parameters["trnOrderNumber"], out orderId))
                return Content("");

            var sb = new StringBuilder();
            sb.AppendLine("Bambora response notification:");
            foreach (var parameter in parameters)
            {
                sb.AppendFormat("{0}: {1}{2}", parameter.Key, parameter.Value, Environment.NewLine);
            }

            var order = await _orderService.GetOrderByIdAsync(orderId);
            if (order != null)
            {
                //order note
                await _orderService.InsertOrderNoteAsync(new OrderNote()
                {
                    Note = sb.ToString(),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                //validate order total
                decimal total;
                if (!decimal.TryParse(parameters["trnAmount"], out total))
                {
                    await _logger.ErrorAsync($"Bambora response notification. {parameters["messageText"]} for the order #{orderId}");
                    return Content("");
                }

                if (!Math.Round(total, 2).Equals(Math.Round(order.OrderTotal, 2)))
                {
                    await _logger.ErrorAsync($"Bambora response notification. Returned order total {total} doesn't equal order total {order.OrderTotal} for the order #{orderId}");
                    return Content("");
                }

                //change order status
                if (parameters["trnApproved"].Equals("1"))
                {
                    if (_orderProcessingService.CanMarkOrderAsPaid(order))
                    {
                        order.AuthorizationTransactionId = parameters["trnId"];
                        await _orderService.UpdateOrderAsync(order);
                        await _orderProcessingService.MarkOrderAsPaidAsync(order);
                    }
                }
            }
            else
                await _logger.ErrorAsync("Bambora response notification. Order is not found", new NopException(sb.ToString()));

            //nothing should be rendered to visitor
            return Content("");
        }

        #endregion
    }
}