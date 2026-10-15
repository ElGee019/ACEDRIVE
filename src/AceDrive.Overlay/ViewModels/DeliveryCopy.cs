using AceDrive.Overlay.Localization;

namespace AceDrive.Overlay.ViewModels;

/// <summary>
/// City-growth headline shared by the delivery board and the hub page
/// (both Unity screens show the same StoreManager total, ▲ +n / ▼ -n / ▬ 0
/// as three resx variants so translators own the copy).
/// </summary>
internal static class DeliveryCopy
{
    public static string CityGrowth(int growthPerHour) => growthPerHour switch
    {
        > 0 => Strings.Format("DeliveryGrowthUp", growthPerHour),
        < 0 => Strings.Format("DeliveryGrowthDown", growthPerHour),
        _ => Strings.Get("DeliveryGrowthFlat"),
    };
}
