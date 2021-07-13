using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Bambora
{
    public class BamboraPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or Bambora sets merchant ID
        /// </summary>
        public string MerchantId { get; set; }

        /// <summary>
        /// Gets or sets hash key
        /// </summary>
        public string HashKey { get; set; }

        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
    }
}
